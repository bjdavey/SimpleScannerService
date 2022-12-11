using iTextSharp.text;
using iTextSharp.text.pdf;
using Saraff.Twain;
using Saraff.Twain.Aux;
using System.Drawing.Imaging;
using System.IO.Compression;
using Image = System.Drawing.Image;

namespace SimpleScannerService
{
    public class Helpers
    {
        private const string x86Aux = "SimpleScannerService.AUX_x86.exe";
        private const string msilAux = "SimpleScannerService.AUX_MSIL.exe";

        public static string AssemblyLocation()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
            //return Path.GetDirectoryName(this.GetType().Assembly.Location) ?? "";
        }

        public static string AuxX86Path()
        {
            return Path.Combine(AssemblyLocation(), Helpers.x86Aux);
        }
        public static string AuxMSILPath()
        {
            return Path.Combine(AssemblyLocation(), Helpers.msilAux);
        }

        public static void _twain32_MemXferEvent(object sender, Twain32.MemXferEventArgs e)
        {
            if (TiffMemXfer.Current != null)
            {
                long _bitsPerRow = e.ImageInfo.BitsPerPixel * e.ImageMemXfer.Columns;
                long _bytesPerRow = Math.Min(e.ImageMemXfer.BytesPerRow, (_bitsPerRow >> 3) + ((_bitsPerRow & 0x07) != 0 ? 1 : 0));
                using (MemoryStream _stream = new MemoryStream())
                {
                    for (int i = 0; i < e.ImageMemXfer.Rows; i++)
                    {
                        _stream.Position = 0;
                        _stream.Write(e.ImageMemXfer.ImageData, (int)(e.ImageMemXfer.BytesPerRow * i), (int)_bytesPerRow);
                        TiffMemXfer.Current.Strips.Add(TiffMemXfer.Current.Writer.WriteData(_stream.ToArray()));
                        TiffMemXfer.Current.StripByteCounts.Add((uint)_stream.Length);
                    }
                }
            }
            else
            {
                MemFileXfer.Current.Writer.Write(e.ImageMemXfer.ImageData);
            }
        }


        public static SourceDetails? GetSourceDetails(ITwain32 twain, string SourceName)
        {
            SourceDetails? sourceDetails = null;
            for (var i = 0; i < twain.SourcesCount; i++)
            {
                if (SourceName == twain.GetSourceProductName(i))
                {
                    twain.SourceIndex = i;
                    twain.OpenDataSource();

                    var pixelTypesEnum = twain.Capabilities.PixelType.Get();
                    var pixelTypes = new List<string>();
                    for (int x = 0; x < pixelTypesEnum.Count; x++)
                    {
                        pixelTypes.Add(pixelTypesEnum[x].ToString() ?? "");
                    }

                    var supportedSizesEnum = twain.Capabilities.SupportedSizes.Get();
                    var supportedSizes = new List<string>();
                    for (int x = 0; x < supportedSizesEnum.Count; x++)
                    {
                        supportedSizes.Add(supportedSizesEnum[x].ToString() ?? "");
                    }

                    var xResolutionEnum = twain.Capabilities.XResolution.Get();
                    var xResolutions = new List<int>();
                    for (int x = 0; x < xResolutionEnum.Count; x++)
                    {
                        xResolutions.Add(Convert.ToInt32(xResolutionEnum[x]));
                    }

                    sourceDetails = new SourceDetails()
                    {
                        SupportDuplex = twain.Capabilities.DuplexEnabled.GetDefault(),
                        SupportedColors = pixelTypes.ToArray(),
                        SupportedPaperSizes = supportedSizes.ToArray(),
                        SupportedResolutions = xResolutions.ToArray(),
                    };
                    return sourceDetails;
                }
            }
            return null;
        }

        public static Stream ToStream(Image image)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            image.Dispose();
            return stream;
        }

        private byte[] CreateZipCollection(List<Image> images)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    for (var i = 0; i < images.Count; i++)
                    {
                        var img = images[i];
                        var name = $"{i + 1}.jpeg";
                        var entry = archive.CreateEntry(name);
                        using (var entryStream = entry.Open())
                        {
                            using (var imgStream = ToStream(img))
                            {
                                imgStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
                ms.Position = 0;
                return ms.ToArray(); ;
            }
        }

        private static byte[] CreatePdfDocument(List<Image> images)
        {
            byte[] pdfBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4);
                PdfWriter writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();
                images.ForEach(image =>
                {
                    doc.NewPage();
                    iTextSharp.text.Image img = iTextSharp.text.Image.GetInstance(image, ImageFormat.Jpeg);
                    img.Alignment = Element.ALIGN_CENTER;
                    img.ScaleToFit(doc.PageSize.Width - 10, doc.PageSize.Height - 10);
                    image.Dispose();
                });
                doc.Close();
                pdfBytes = ms.ToArray();
            }
            return pdfBytes;
        }

    }
}
