using System;

namespace SeAudioVisualizer.SpaceEngineers
{
    public interface IGeneratedTexture : IDisposable
    {
        string Name { get; }
        string MaterialKey { get; }
        int Width { get; }
        int Height { get; }
        byte[] BeginUpdate();
        void EndUpdate(byte[] pixels);
    }

    public interface IGeneratedTextureFactory
    {
        int MaxTextureSide { get; }
        IGeneratedTexture Create(string nameHint, int width, int height);
    }
}
