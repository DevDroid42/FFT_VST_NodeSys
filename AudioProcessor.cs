using Jacobi.Vst.Core;
using Jacobi.Vst.Plugin.Framework;
using Jacobi.Vst.Plugin.Framework.Plugin;
using System;
using System.Diagnostics;
using VstNetAudioPlugin.Dsp;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NodeSysCore;
using System.Numerics;

namespace VstNetAudioPlugin
{
    /// <summary>
    /// This object performs audio processing for your plugin.
    /// </summary>
    internal sealed class AudioProcessor : VstPluginAudioProcessor, IVstPluginBypass
    {
        /// <summary>
        /// TODO: assign the input count.
        /// </summary>
        private const int AudioInputCount = 2;
        /// <summary>
        /// TODO: assign the output count.
        /// </summary>
        private const int AudioOutputCount = 2;
        /// <summary>
        /// TODO: assign the tail size.
        /// </summary>
        private const int InitialTailSize = 0;

        // TODO: change this to your specific needs.
        private readonly VstTimeInfoFlags _defaultTimeInfoFlags = VstTimeInfoFlags.ClockValid;
        // set after the plugin is opened
        private IVstHostSequencer? _sequencer;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AudioProcessor(IVstPluginEvents pluginEvents, PluginParameters parameters)
            : base(AudioInputCount, AudioOutputCount, InitialTailSize, noSoundInStop: false)
        {
            Throw.IfArgumentIsNull(pluginEvents, nameof(pluginEvents));
            Throw.IfArgumentIsNull(parameters, nameof(parameters));

            // one set of parameters is shared for both channels.
            Left = new Delay(parameters.DelayParameters);
            Right = new Delay(parameters.DelayParameters);

            pluginEvents.Opened += Plugin_Opened;
        }

        internal Delay Left { get; private set; }
        internal Delay Right { get; private set; }

        /// <summary>
        /// Override the default implementation to pass it through to the delay.
        /// </summary>
        public override float SampleRate
        {
            get { return Left.SampleRate; }
            set
            {
                Left.SampleRate = value;
                Right.SampleRate = value;
            }
        }

        private VstTimeInfo? _timeInfo;
        /// <summary>
        /// Gets the current time info.
        /// </summary>
        /// <remarks>The Time Info is refreshed with each call to Process.</remarks>
        internal VstTimeInfo? TimeInfo
        {
            get
            {
                if (_timeInfo == null && _sequencer != null)
                {
                    _timeInfo = _sequencer.GetTime(_defaultTimeInfoFlags);
                }

                return _timeInfo;
            }
        }

        private void Plugin_Opened(object? sender, EventArgs e)
        {
            var plugin = (VstPlugin?)sender;

            // A reference to the host is only available after 
            // the plugin has been loaded and opened by the host.
            _sequencer = plugin?.Host?.GetInstance<IVstHostSequencer>();
        }

        /// <summary>
        /// Called by the host to allow the plugin to process audio samples.
        /// </summary>
        /// <param name="inChannels">Never null.</param>
        /// <param name="outChannels">Never null.</param>
        public override void Process(VstAudioBuffer[] inChannels, VstAudioBuffer[] outChannels)
        {
            // by resetting the time info each cycle, accessing the TimeInfo property will fetch new info.
            _timeInfo = null;

            if (!Bypass)
            {
                // check assumptions
                Debug.Assert(outChannels.Length == inChannels.Length);

                // TODO: Implement your audio (effect) processing here.

                for (int i = 0; i < outChannels.Length; i++)
                {                    
                    Process(i % 2 == 0 ? Left : Right,
                        inChannels[i], outChannels[i]);
                }
                base.Process(inChannels, outChannels);
            }
            else
            {
                // calling the base class transfers input samples to the output channels unchanged (bypass).
                base.Process(inChannels, outChannels);
            }
        }


        // Create arrays for complex data and FFT result
        const int bufferSize = 2048;
        Complex32[] buffer = new Complex32[bufferSize]; // Using Complex32 type for complex numbers
        int head = 0;
        // process a single audio channel
        private void Process(Delay delay, VstAudioBuffer input, VstAudioBuffer output)
        {
            for (int i = 0; i < input.SampleCount; i++)
            {
                buffer[head++] = input[i];
                if (head == bufferSize - 1)
                {
                    head = 0;
                    break;
                };
            }
            if (head != 0) return;

            // Perform FFT
            Fourier.Forward(buffer, FourierOptions.Default);

            double[] magnitudes = new double[buffer.Length / 8];
            int maxIndex = 0;
            double maxMag = 0;
            for (int i = 0; i < magnitudes.Length; i++)
            {
                magnitudes[i] = buffer[i].Magnitude;
                if (magnitudes[i] > maxMag)
                {
                    maxIndex = i;
                    maxMag = magnitudes[i];
                }
            }

            byte[] colorBuffer = new byte[magnitudes.Length];
            for (int i = 0; i < colorBuffer.Length; i++)
            {
                colorBuffer[i] = (byte)(magnitudes[i] / 10 * 255 + 0.5);
            }

            Delay.networking.SendByteArray("FFT" + delay.GetIndex(), colorBuffer);

        }

        #region IVstPluginBypass Members

        public bool Bypass { get; set; }

        #endregion
    }
}
