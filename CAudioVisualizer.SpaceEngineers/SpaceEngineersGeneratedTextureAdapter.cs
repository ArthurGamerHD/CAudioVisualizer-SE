using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace SeAudioVisualizer.SpaceEngineers
{
    /// <summary>
    /// Thin adapter over MyAPIGateway.GeneratedTextures. This source is intentionally
    /// contained in the Space Engineers mixin so the Core mixin remains game-agnostic.
    /// </summary>
    public sealed class SpaceEngineersGeneratedTextureFactory : IGeneratedTextureFactory
    {
        public int MaxTextureSide
        {
            get
            {
                return MyAPIGateway.GeneratedTextures == null
                    ? 0
                    : MyAPIGateway.GeneratedTextures.MaxTextureSide;
            }
        }

        public IGeneratedTexture Create(string nameHint, int width, int height)
        {
            if (MyAPIGateway.GeneratedTextures == null || !MyAPIGateway.GeneratedTextures.IsSupported)
                return null;

            IMyGeneratedTexture texture = MyAPIGateway.GeneratedTextures.Create(
                nameHint,
                new Vector2I(width, height),
                MyGeneratedTextureFormat.Srgb);

            return texture == null ? null : new SpaceEngineersGeneratedTexture(texture);
        }
    }

    public sealed class SpaceEngineersGeneratedTexture : IGeneratedTexture
    {
        readonly IMyGeneratedTexture _texture;

        public SpaceEngineersGeneratedTexture(IMyGeneratedTexture texture)
        {
            _texture = texture;
        }

        public string Name
        {
            get { return _texture.Name; }
        }

        public string MaterialKey
        {
            get { return _texture.Name; }
        }

        public int Width
        {
            get { return _texture.Size.X; }
        }

        public int Height
        {
            get { return _texture.Size.Y; }
        }

        public byte[] BeginUpdate()
        {
            return _texture.BeginUpdate();
        }

        public void EndUpdate(byte[] pixels)
        {
            _texture.EndUpdate(pixels);
        }

        public void Dispose()
        {
            _texture.Dispose();
        }
    }
}
