using System;
using System.ComponentModel;
using NodeSysCore;

namespace VstNetAudioPlugin.Dsp
{
    /// <summary>
    /// This is an example of a Digital Sound Processing component you could have in your plugin.
    /// </summary>
    internal sealed class Delay
    {
        private float[] _delayBuffer;
        private int _bufferIndex;
        public int _bufferLength;

        public static UdpNetworking networking;

        private readonly DelayParameters _parameters;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public Delay(DelayParameters parameters)
        {
            if(networking == null)
            {
                networking = new UdpNetworking("127.0.0.1", 11000);
            }
            _delayBuffer = Array.Empty<float>();
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

            // when the delay time parameter value changes, we like to know about it.
            _parameters.DelayTimeMgr.PropertyChanged += DelayTimeMgr_PropertyChanged;
        }

        private void DelayTimeMgr_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Object.ReferenceEquals(_parameters.DelayTimeMgr, sender))
            {
                SetBufferLength();
            }
        }

        private void SetBufferLength()
        {
            // logical buffer length
            _bufferLength = (int)(_parameters.DelayTimeMgr.CurrentValue * _sampleRate / 1000);

            //networking.SendMessage(0, "test", BitConverter.GetBytes(_bufferLength));
        }

        private float _sampleRate;
        /// <summary>
        /// Gets or sets the sample rate.
        /// </summary>
        public float SampleRate
        {
            get { return _sampleRate; }
            set
            {
                _sampleRate = value;

                // allocate buffer for max delay time
                int bufferLength = (int)(_parameters.DelayTimeMgr.ParameterInfo.MaxInteger * _sampleRate / 1000);
                _delayBuffer = new float[bufferLength];

                SetBufferLength();
            }
        }

        /// <summary>
        /// Processes the <paramref name="sample"/> using a delay effect.
        /// </summary>
        /// <param name="sample">A single sample.</param>
        /// <returns>Returns the new value for the sample.</returns>
        public float ProcessSample(float sample)
        {
            if (_delayBuffer == null)
                return sample;

            // process output
            float output = (_parameters.DryLevelMgr.CurrentValue * sample) +
                (_parameters.WetLevelMgr.CurrentValue * _delayBuffer[_bufferIndex]);

            // process delay buffer
            _delayBuffer[_bufferIndex] = sample +
                (_parameters.FeedbackMgr.CurrentValue * _delayBuffer[_bufferIndex]);

            _bufferIndex++;

            // manage current buffer position
            if (_bufferIndex >= _bufferLength)
            {
                _bufferIndex = 0;
            }

            return output;
        }

        public string GetIndex()
        {
            return ((int)(_parameters.DryLevelMgr.CurrentValue * 100)).ToString();
        }
    }
}
