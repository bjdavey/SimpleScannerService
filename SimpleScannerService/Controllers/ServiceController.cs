using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NTwain;
using NTwain.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Image = System.Drawing.Image;
using Rectangle = System.Drawing.Rectangle;

namespace SimpleScannerService.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("ScannerService/[action]")]
    public class ServiceController : ControllerBase
    {

        public ServiceController()
        {

        }

        [HttpGet]
        public ActionResult Test()
        {
            try
            {
                return Ok("HelloWorld");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }

        [HttpGet]
        public ActionResult GetAvailableScanners()
        {
            try
            {
                NTwain.PlatformInfo.Current.PreferNewDSM = false;
                var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
                var twainSession = new TwainSession(appId);
                twainSession.Open();
                var list = twainSession.GetSources().ToList().Select(source => source.Name).ToArray();
                twainSession.Close();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }

        [HttpGet]
        public ActionResult GetScannerDetails([FromQuery] string ScannerName)
        {
            try
            {
                NTwain.PlatformInfo.Current.PreferNewDSM = false;
                var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
                var twainSession = new TwainSession(appId);

                twainSession.Open();

                var scanner = twainSession.GetSources().FirstOrDefault(x => x.Name == ScannerName);

                if (scanner == null)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.ScannerNotAvailable,
                        Message = $"'{ScannerName}' scanner is not available"
                    });
                }

                scanner.Open();

                if (!scanner.IsOpen)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{scanner.Name}'"
                    });
                }

                var result = new
                {
                    Name = scanner.Name,
                    SupportDuplex = scanner.Capabilities.CapDuplexEnabled.IsSupported,
                    SupportedColors = scanner.Capabilities.ICapPixelType?.GetValues()?.Select(x => x.ToString()),
                    SupportedPaperSizes = scanner.Capabilities.ICapSupportedSizes?.GetValues()?.Select(x => x.ToString()),
                    SupportedResolutions = scanner.Capabilities.ICapXResolution?.GetValues()?.Where(dpi => (dpi % 50) == 0).Select(x => x.Whole)
                };

                scanner.Close();
                twainSession.Close();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }


        [HttpGet]
        public ActionResult GetAvailableScannersDetailed()
        {
            try
            {
                NTwain.PlatformInfo.Current.PreferNewDSM = false;
                var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
                var twainSession = new TwainSession(appId);

                twainSession.Open();

                var list = new List<dynamic>();

                twainSession.GetSources().ToList().ForEach(source =>
                {
                    source.Open();
                    if (source.IsOpen)
                    {
                        var item = new
                        {
                            Name = source.Name,
                            SupportDuplex = source.Capabilities.CapDuplexEnabled.IsSupported,
                            SupportedColors = source.Capabilities.ICapPixelType?.GetValues()?.Select(x => x.ToString()),
                            SupportedPaperSizes = source.Capabilities.ICapSupportedSizes?.GetValues()?.Select(x => x.ToString()),
                            SupportedResolutions = source.Capabilities.ICapXResolution?.GetValues()?.Where(dpi => (dpi % 50) == 0).Select(x => x.Whole)
                        };
                        list.Add(item);
                        source.Close();
                    }
                });
                twainSession.Close();
                return Ok(list);

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }


        [HttpGet]
        public ActionResult Scan(
            [FromQuery] string? ScannerName,
            [FromQuery] FileTypes? FileType, //JPEG,PDF,ZIP
            [FromQuery] bool? DuplexEnabled,
            [FromQuery] PixelType? Color, //RGB, BlackWhite, Gray
            [FromQuery] SupportedSize? PaperSize, //A4,A3...etc
            [FromQuery] int? Resolution
        )
        {
            try
            {
                NTwain.PlatformInfo.Current.PreferNewDSM = false;
                var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
                var twainSession = new TwainSession(appId);

                twainSession.Open();

                List<Image> scannedImages = new List<Image>();
                twainSession.DataTransferred += (s, e) =>
                {
                    switch (e.TransferType)
                    {
                        case XferMech.Native:
                            if (e.NativeData != IntPtr.Zero)
                            {
                                var stream = e.GetNativeImageStream();
                                if (stream != null)
                                {
                                    scannedImages.Add(Image.FromStream(stream));
                                }
                            }
                            break;
                        case XferMech.File:
                            if (String.IsNullOrEmpty(e.FileDataPath))
                            {
                                var img = new Bitmap(e.FileDataPath);
                                scannedImages.Add(img);
                            }
                            break;
                        case XferMech.Memory:
                            if (e.MemoryData != null && e.MemoryData.Length > 0)
                            {
                                scannedImages.Add(ToImage(e.MemoryData, e.ImageInfo));
                            }
                            break;
                    }

                };

                var TransferError = false;
                twainSession.TransferError += (s, e) =>
                {
                    TransferError = true;
                };

                DataSource scanner = twainSession.DefaultSource;
                if (scanner == null)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.NoScannerFound,
                        Message = $"Could not find any scanner"
                    });
                }

                if (ScannerName != null)
                {
                    var value = twainSession.GetSources().FirstOrDefault(x => x.Name == ScannerName);
                    if (value != null)
                    {
                        scanner = value;
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.ScannerNotAvailable,
                            Message = $"'{ScannerName}' scanner is not available"
                        });
                    }
                }
                scanner.Open();
                if (!scanner.IsOpen)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{scanner.Name}'"
                    });
                }

                if (scanner.Capabilities.ACapXferMech.IsSupported)
                {
                    scanner.Capabilities.ACapXferMech.SetValue(XferMech.Native);
                    //scanner.Capabilities.ACapXferMech.SetValue(XferMech.Memory);
                }
                if (scanner.Capabilities.ICapXferMech.IsSupported)
                {
                    scanner.Capabilities.ICapXferMech.SetValue(XferMech.Native);
                    //scanner.Capabilities.ICapXferMech.SetValue(XferMech.Memory);
                }
                //if (scanner.Capabilities.CapClearPage.IsSupported)
                //{
                //    scanner.Capabilities.CapClearPage.SetValue(BoolType.True);
                //}
                //if (scanner.Capabilities.CapClearBuffers.IsSupported)
                //{
                //    scanner.Capabilities.CapClearBuffers.SetValue(ClearBuffer.Clear);
                //}

                if (scanner.Capabilities.ICapAutoDiscardBlankPages.IsSupported)
                {
                    scanner.Capabilities.ICapAutoDiscardBlankPages.SetValue(BlankPage.Auto);
                }

                if (scanner.Capabilities.CapDuplexEnabled.IsSupported)
                {
                    if (DuplexEnabled == null) DuplexEnabled = true;
                    var value = (bool)DuplexEnabled ? BoolType.True : BoolType.False;
                    scanner.Capabilities.CapDuplexEnabled.SetValue(value);
                }

                if (scanner.Capabilities.ICapPixelType.IsSupported)
                {
                    if (Color == null) Color = PixelType.RGB;
                    scanner.Capabilities.ICapPixelType.SetValue((PixelType)Color);
                }

                if (PaperSize != null && scanner.Capabilities.ICapSupportedSizes.IsSupported)
                {
                    scanner.Capabilities.ICapSupportedSizes.SetValue((SupportedSize)PaperSize);
                }

                if (scanner.Capabilities.ICapXResolution.IsSupported && scanner.Capabilities.ICapYResolution.IsSupported)
                {
                    if (Resolution == null) Resolution = 200;
                    var value = (TWFix32)Resolution;
                    scanner.Capabilities.ICapXResolution.SetValue(value);
                    scanner.Capabilities.ICapYResolution.SetValue(value);
                }

                // Start Scan
                scanner.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero);
                //scanner.Enable(SourceEnableMode.ShowUI, false, IntPtr.Zero);

                scanner.Close();
                twainSession.Close();

                if (TransferError)
                {
                    return StatusCode(StatusCodes.Status417ExpectationFailed, new
                    {
                        Code = Errors.TransferError,
                        Message = $"Transfer Error"
                    });
                }

                if (scannedImages == null || scannedImages.Count == 0)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.NoImagesScanned,
                        Message = $"No images could be scanned"
                    });
                }

                switch (FileType)
                {
                    case null:
                    case FileTypes.PDF:
                        return File(CreatePdfDocument(scannedImages), "application/pdf");
                    case FileTypes.JPEG:
                        return File(ToStream(scannedImages.FirstOrDefault()), "image/jpeg");
                    case FileTypes.ZIP:
                        return File(CreateZipCollection(scannedImages), "application/zip");
                    default:
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.InvalidFileType,
                            Message = $"Invalid file type: '{FileType}'"
                        });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }

        private Stream ToStream(Image image)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            image.Dispose();
            return stream;
        }


        private Image ToImage(byte[] bytes, TWImageInfo info)
        {
            byte[] newData = new byte[bytes.Length];

            for (int x = 0; x < bytes.Length; x += 3)
            {
                byte[] pixel = new byte[3];
                Array.Copy(bytes, x, pixel, 0, 3);

                byte r = pixel[0];
                byte g = pixel[1];
                byte b = pixel[2];

                byte[] newPixel = new byte[] { r, g, b };

                Array.Copy(newPixel, 0, newData, x, 3);
            }

            bytes = newData;

            var bmp = new Bitmap(info.ImageWidth, info.ImageLength, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                    bmp.Width,
                    bmp.Height),
                ImageLockMode.WriteOnly,
                bmp.PixelFormat);

            IntPtr pNative = bmpData.Scan0;
            Marshal.Copy(bytes, 0, pNative, bytes.Length);

            bmp.UnlockBits(bmpData);

            return bmp;
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