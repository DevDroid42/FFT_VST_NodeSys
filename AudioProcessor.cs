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

        double[] notes =
        {16.35,17.32,18.35,19.45,20.6,21.83,23.12,24.5,25.96,27.5,29.14,30.87
        ,32.7,34.65,36.71,38.89,41.2,43.65,46.25,49,51.91,55,58.27,61.74
        ,65.41,69.3,73.42,77.78,82.41,87.31,92.5,98,103.83,110,116.54,123.47
        ,130.81,138.59,146.83,155.56,164.81,174.61,185,196,207.65,220,233.08,246.94
        ,261.63,277.18,293.66,311.13,329.63,349.23,369.99,392,415.3,440,466.16,493.88
        ,523.25,554.37,587.33,622.25,659.25,698.46,739.99,783.99,830.61,880,932.33,987.77
        ,1046.5,1108.73,1174.66,1244.51,1318.51,1396.91,1479.98,1567.98,1661.22,1760,1864.66,1975.53
        ,2093,2217.46,2349.32,2489,2637,2793.83,2959.96,3135.96,3322.44,3520,3729.31,3951
        ,4186,4434.92,4698.63,4978,5274,5587.65,5919.91,6271.93,6644.88,7040,7458.62,7902.13
        ,8372,8869.84,9397.26,9956,10548,11175.3,11839.82,12543.86,13289.76,14080,14917.24,15804.26
        ,16744,17739.68,18794.52,19912};
        float[] noteIntensity = { };
        byte[] colorBuffer = { };

        // Create arrays for complex data and FFT result
        const int bufferSize = 1000;
        Complex32[] buffer = new Complex32[bufferSize]; // Using Complex32 type for complex numbers
        int head = 0;
        double[] magnitudes = new double[bufferSize / 2];

        // process a single audio channel
        private void Process(Delay delay, VstAudioBuffer input, VstAudioBuffer output)
        {
            if(noteIntensity.Length == 0)
            {
                noteIntensity = new float[notes.Length];
            }
            if(colorBuffer.Length == 0)
            {
                colorBuffer = new byte[notes.Length];
            }
            
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
            
            double maxMag = 0;
            for (int i = 0; i < magnitudes.Length; i++)
            {
                magnitudes[i] = buffer[i].Magnitude;
                if (magnitudes[i] > maxMag)
                {
                    maxMag = magnitudes[i];
                }
            }


            float hzPerSample = SampleRate / bufferSize;
            float currentHz = 0;
            int currNote = 0;
            int sampleCount = 0;
            Array.Clear(noteIntensity, 0, noteIntensity.Length);
            for (int i = 0; i < magnitudes.Length; i++)
            {
                if (currNote >= notes.Length - 1) break;

                currentHz += hzPerSample;

                if (currentHz < notes[currNote])
                {
                    sampleCount += 1;
                    noteIntensity[currNote] += (float)magnitudes[i];
                }

                if (currentHz >= notes[currNote])
                {

                    noteIntensity[currNote] /= sampleCount;
                    sampleCount = 0;
                    currNote += 1;
                }
            }

            for (int i = 0; i < noteIntensity.Length; i++)
            {
                colorBuffer[i] = (byte)(Math.Clamp(noteIntensity[i], 0, 1) * 255 + 0.5);
            }

            Delay.networking.SendByteArray("FFT" + delay.GetIndex(), colorBuffer);

        }

        #region IVstPluginBypass Members

        public bool Bypass { get; set; }

        #endregion
    }
}
