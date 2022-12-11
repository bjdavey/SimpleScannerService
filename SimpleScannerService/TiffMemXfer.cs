using Saraff.Tiff;

namespace SimpleScannerService
{
    public sealed class TiffMemXfer
    {

        public static void Create(int bufferSize)
        {
            TiffMemXfer.Current = new TiffMemXfer
            {
                Writer = TiffWriter.Create(File.Create(string.Format("MemXferTransfer_{0}.tif", DateTime.Now.ToString("yyyyMMddHHmmss")), bufferSize)),
                Strips = new List<TiffHandle>(),
                StripByteCounts = new List<uint>()
            };
            TiffMemXfer.Current.Handle = TiffMemXfer.Current.Writer.WriteHeader();
        }

        public static void Dispose()
        {
            if (TiffMemXfer.Current != null)
            {
                TiffMemXfer.Current = null;
            }
        }

        public static TiffMemXfer Current
        {
            get;
            private set;
        }

        public TiffHandle Handle
        {
            get;
            set;
        }

        public TiffWriter Writer
        {
            get;
            private set;
        }

        public List<TiffHandle> Strips
        {
            get;
            private set;
        }

        public List<uint> StripByteCounts
        {
            get;
            private set;
        }

        public ushort[] ColorMap
        {
            get;
            set;
        }
    }

    public sealed class MemFileXfer
    {

        public static void Create(int bufferSize, string ext)
        {
            if (MemFileXfer.Current != null && MemFileXfer.Current.Writer != null)
            {
                MemFileXfer.Current.Writer.BaseStream.Dispose();
            }
            MemFileXfer.Current = new MemFileXfer
            {
                Writer = new BinaryWriter(File.Create(string.Format("MemFileXferTransfer_{0}.{1}", DateTime.Now.ToString("yyyyMMddHHmmss"), ext), bufferSize))
            };
        }

        public static void Dispose()
        {
            if (MemFileXfer.Current == null)
            {
                MemFileXfer.Current = null;
            }
        }

        public static MemFileXfer Current
        {
            get;
            private set;
        }

        public BinaryWriter Writer
        {
            get;
            private set;
        }
    }

}
