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

        public static FileStream CreateZipCollection(List<Image> images)
        {
            var fileName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var tmpFile = Path.Combine(Helpers.TempFolder(), $"tmp-{fileName}.zip");
            var fs = new FileStream(tmpFile, FileMode.CreateNew);
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, true))
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
            fs.Position = 0;
            return fs;
        }

        public static FileStream CreatePdfDocument(List<Image> images)
        {
            var fileName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var tmpFile = Path.Combine(Helpers.TempFolder(), $"tmp-{fileName}.pdf");
            var fs = new FileStream(tmpFile, FileMode.CreateNew);
            Document doc = new Document(PageSize.A4);
            PdfWriter.GetInstance(doc, fs);
            doc.Open();
            images.ForEach(image =>
            {
                doc.NewPage();
                var img = iTextSharp.text.Image.GetInstance(image, ImageFormat.Jpeg);
                img.Alignment = Element.ALIGN_CENTER;
                img.ScaleToFit(doc.PageSize.Width - 10, doc.PageSize.Height - 10);
                doc.Add(img);
            });
            doc.Close();
            foreach (var i in images)
            {
                i.Dispose();
            }
            fs.Position = 0;
            return fs;
        }



    }
}
