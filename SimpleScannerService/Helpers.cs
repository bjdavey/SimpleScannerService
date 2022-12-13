using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Drawing.Imaging;
using System.IO.Compression;
using static System.Environment;
using Image = System.Drawing.Image;

namespace SimpleScannerService
{
    public class Helpers
    {
        public static string TempFolder()
        {
            var programDataFolder = GetFolderPath(SpecialFolder.CommonApplicationData);
            return System.IO.Directory.CreateDirectory(Path.Combine(programDataFolder, "SimpleScannerService", "tmp")).FullName;
        }
        public static void CleanTempFolder()
        {
            Directory.Delete(TempFolder(), true);
        }

        public static Stream ToStream(Image image)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            image.Dispose();
            return stream;
        }

        public static byte[] CreateZipCollection(List<Image> images)
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

        public static byte[] CreatePdfDocument(List<Image> images)
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
                    doc.Add(img);
                    image.Dispose();
                });
                writer.Flush();
                writer.Close();
                doc.Close();
                pdfBytes = ms.ToArray();
            }
            return pdfBytes;
        }



    }
}
