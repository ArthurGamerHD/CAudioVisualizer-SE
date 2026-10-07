namespace SeAudioVisualizer.SpaceEngineers
{
    public sealed class PublishedGeneratedTexture
    {
        public IGeneratedTexture Texture { get; private set; }
        public long FrameSequence { get; private set; }

        public PublishedGeneratedTexture(IGeneratedTexture texture, long frameSequence)
        {
            Texture = texture;
            FrameSequence = frameSequence;
        }
    }
}
