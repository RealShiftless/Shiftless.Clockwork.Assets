using Shiftless.Common.Mathematics;
using Shiftless.Common.Serialization;

namespace Shiftless.Clockwork.Assets
{
    public readonly struct Texture2DData(int width, int height, ColorMode colorMode, byte[] data, Color[]? palette = null)
    {
        // Constants
        public const string HEADER = "Tx2D";


        // Values
        public readonly int Width = width;
        public readonly int Height = height;

        public readonly ColorMode ColorMode = colorMode;

        private readonly byte[] _data = data;
        private readonly Color[]? _palette = palette;


        // Properties
        public IEnumerable<byte> Raw => _data;

        public int Pixels => ColorMode switch
        {
            ColorMode.Lum1 => _data.Length * 8,
            ColorMode.Lum2 => _data.Length * 4,
            ColorMode.Lum4 => _data.Length * 2,
            ColorMode.Lum8 => _data.Length,

            ColorMode.LumA8 => _data.Length / 2,

            ColorMode.Palette1 => _data.Length * 8,
            ColorMode.Palette2 => _data.Length * 4,
            ColorMode.Palette4 => _data.Length * 2,
            ColorMode.Palette8 => _data.Length,

            ColorMode.RGB565 => _data.Length / 2,
            ColorMode.RGB8 => _data.Length / 3,
            ColorMode.RGBA8 => _data.Length / 4,

            _ => throw new Exception($"Color mode {ColorMode} went unhandled!")
        };


        // Func
        public static Texture2DData LoadFromFile(string filePath)
        {
            ByteStream stream = new(filePath);

            if (stream.ReadString(4) != "Tx2D")
                throw new Exception($"Invallid file header for Texture2D {filePath}!");

            int width = (int)stream.ReadUInt32();
            int height = (int)stream.ReadUInt32();
            ColorMode colorMode = (ColorMode)stream.ReadInt32();

            Color[]? palette = null;
            if (stream.ReadString(4) == "plte")
            {
                if (!(colorMode >= ColorMode.Palette1 && colorMode <= ColorMode.Palette8))
                    throw new Exception($"Invallid file header for Texture2D {filePath}!");

                int colors = 1 << (colorMode - (ColorMode.Palette1 - 1) << 1);

                palette = new Color[colors];
                for (int i = 0; i < colors; i++)
                    palette[i] = new(stream.ReadUInt32());
            }

            byte[] data = stream.ReadRemaining();

            return new(width, height, colorMode, data, palette);
        }

        public byte[] GetColorData()
        {
            byte[] data = new byte[Pixels * 4];

            for (int i = 0; i < Pixels; i++)
            {
                Color color = GetPixel(i);

                data[i * 4 + 0] = color.R;
                data[i * 4 + 1] = color.G;
                data[i * 4 + 2] = color.B;
                data[i * 4 + 3] = color.A;
            }

            return data;
        }
        public Color[] GetPixels()
        {
            Color[] pixels = new Color[Pixels];

            for (int i = 0; i < Pixels; i++)
                pixels[i] = GetPixel(i);

            return pixels;
        }


        public Color GetPixel(int pixel) => ColorMode switch
        {
            ColorMode.Lum1 => GetLum(pixel, PixelsPerByte.Ppb8),
            ColorMode.Lum2 => GetLum(pixel, PixelsPerByte.Ppb4),
            ColorMode.Lum4 => GetLum(pixel, PixelsPerByte.Ppb2),
            ColorMode.Lum8 => GetLum(pixel, PixelsPerByte.Ppb1),

            ColorMode.LumA8 => GetLumA8(pixel),

            ColorMode.Palette1 => GetPalette(pixel, PixelsPerByte.Ppb8),
            ColorMode.Palette2 => GetPalette(pixel, PixelsPerByte.Ppb4),
            ColorMode.Palette4 => GetPalette(pixel, PixelsPerByte.Ppb2),
            ColorMode.Palette8 => GetPalette(pixel, PixelsPerByte.Ppb1),

            ColorMode.RGB565 => GetRGB565(pixel),
            ColorMode.RGB8 => GetRGB8(pixel),
            ColorMode.RGBA8 => GetRGBA8(pixel),

            _ => throw new Exception($"Color mode {ColorMode} went unhandled!")
        };

        public byte GetByte(int i) => _data[i];

        /// <summary>
        /// Gets a packed pixel value
        /// </summary>
        /// <param name="pixel">The index of the pixel.</param>
        /// <param name="ppb">The amount of pixels per bit</param>
        /// <returns>The value of said pixel</returns>
        public byte GetPackedPixelIndex(int pixel, PixelsPerByte ppb)
        {
            int i = pixel / (int)ppb;
            int bpp = 8 / (int)ppb;
            int shift = 8 - bpp * (pixel + 1);

            int mask = 1 << (int)ppb;

            return (byte)(_data[i] << shift & mask);
        }

        private Color GetLum(int pixel, PixelsPerByte ppb)
        {
            byte indexedValue = GetPackedPixelIndex(pixel, ppb);

            int maxValue = (1 << (int)ppb) - 1;

            byte grayScale = (byte)((indexedValue * 255 + maxValue / 2) / maxValue);

            return new Color(grayScale);
        }

        private Color GetLumA8(int pixel)
        {
            int i = pixel * 2;

            byte lum = _data[i + 0];
            byte alpha = _data[i + 1];

            return new(lum, alpha);
        }

        private Color GetPalette(int pixel, PixelsPerByte ppb)
        {
            if (_palette == null)
                return GetLum(pixel, ppb);

            byte indexedValue = GetPackedPixelIndex(pixel, ppb);
            return _palette[indexedValue];
        }

        private Color GetRGB565(int pixel)
        {
            // First get the pixel index
            int i = pixel * 2;

            // Get the packed value
            ushort value = (ushort)(_data[i + 0] << 8 | _data[i + 1]);

            // Get the components
            int r5 = value >> 11 & 0x1F;
            int g6 = value >> 5 & 0x3F;
            int b5 = value & 0x1F;

            // Convert to 8-bit per channel with proper scaling
            byte r = (byte)((r5 * 255 + 15) / 31);
            byte g = (byte)((g6 * 255 + 31) / 63);
            byte b = (byte)((b5 * 255 + 15) / 31);

            return new(r, g, b);
        }
        private Color GetRGB8(int pixel) => new(_data[pixel * 3 + 0], _data[pixel * 3 + 1], _data[pixel * 3 + 2]);
        private Color GetRGBA8(int pixel) => new(_data[pixel * 4 + 0], _data[pixel * 4 + 1], _data[pixel * 4 + 2], _data[pixel * 4 + 3]);
    }

    public enum PixelsPerByte
    {
        Ppb1 = 1,
        Ppb2 = 2,
        Ppb4 = 4,
        Ppb8 = 8
    }
}
