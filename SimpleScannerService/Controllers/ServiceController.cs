using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saraff.Twain.Aux;
using System.Drawing;

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
                List<string> list = new List<string>();
                List<string> auxExceptionsList = new List<string>();

                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem)
                {
                    TwainExternalProcess.Execute(AuxService.AuxMSILPath(),
                        twain =>
                        {
                            if (!twain.IsTwain2Supported) return;
                            for (var i = 0; i < twain.SourcesCount; i++)
                            {
                                list.Add(twain.GetSourceProductName(i));
                            }
                        });
                }
                #endregion

                #region AuxX86
                TwainExternalProcess.Execute(AuxService.AuxX86Path(),
                    twain =>
                    {
                        for (var i = 0; i < twain.SourcesCount; i++)
                        {
                            list.Add(twain.GetSourceProductName(i));
                        }
                    });
                #endregion

                var auxException = string.Join(" + ", auxExceptionsList);
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                return Ok(list.Distinct().ToArray());
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
                AuxResponse<SourceDetails> sourceDetails = new AuxResponse<SourceDetails>();

                //Firstly, let's try AuxMSIL
                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem)
                {
                    TwainExternalProcess.Execute(AuxService.AuxMSILPath(),
                        twain =>
                        {
                            if (!twain.IsTwain2Supported) return;
                            sourceDetails = AuxService.GetSourceDetails(twain, ScannerName.Trim(), "AuxMSIL");
                        });
                }
                #endregion
                if (sourceDetails.ExceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                //Let's try AuxX86Path if we couldn't find the source
                if (!sourceDetails.ScannerFound)
                {
                    #region AuxX86
                    TwainExternalProcess.Execute(AuxService.AuxX86Path(),
                        twain =>
                        {
                            sourceDetails = AuxService.GetSourceDetails(twain, ScannerName.Trim(), "AuxX86");
                        });
                    #endregion
                }
                if (sourceDetails.ExceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                var auxException = string.Join(" + ", sourceDetails.AuxExceptionsList);
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                if (!sourceDetails.ScannerFound)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.ScannerNotAvailable,
                        Message = $"'{ScannerName}' scanner is not available"
                    });
                }

                return Ok(sourceDetails.Response);
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
            [FromQuery] string? Color, //RGB, BlackWhite, Gray
            [FromQuery] string? PaperSize, //A4,A3...etc
            [FromQuery] int? Resolution,
            [FromQuery] bool? RemoveBlankPages
        )
        {
            try
            {
                AuxResponse<List<Image>> scannedImages = new AuxResponse<List<Image>>();
                var parameters = new ScanningParameters()
                {
                    Source = ScannerName,
                    DuplexEnabled = DuplexEnabled,
                    Color = Color,
                    PaperSize = PaperSize,
                    Resolution = Resolution,
                    RemoveBlankPages = RemoveBlankPages
                };

                //Firstly, let's try AuxMSIL
                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem)
                {
                    TwainExternalProcess.Execute(AuxService.AuxMSILPath(),
                        twain =>
                        {
                            if (!twain.IsTwain2Supported) return;
                            scannedImages = AuxService.GetScannedImages(twain, "AuxMSIL", parameters);
                        });
                }
                #endregion
                if (scannedImages.ExceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                //Let's try AuxX86Path if we couldn't find the source
                if (!scannedImages.ScannerFound)
                {
                    #region AuxX86
                    TwainExternalProcess.Execute(AuxService.AuxX86Path(),
                        twain =>
                        {
                            scannedImages = AuxService.GetScannedImages(twain, "AuxX86", parameters);
                        });
                    #endregion
                }
                if (scannedImages.ExceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                var auxException = string.Join(" + ", scannedImages.AuxExceptionsList);
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                if (!scannedImages.ScannerFound)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.ScannerNotAvailable,
                        Message = $"'{ScannerName}' scanner is not available"
                    });
                }

                if (scannedImages.Response == null || scannedImages.Response.Count == 0)
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
                        var pdf = Helpers.CreatePdfDocument(scannedImages.Response);
                        Helpers.CleanTempFolder();
                        return File(pdf, "application/pdf");
                    case FileTypes.JPEG:
                        var jpeg = Helpers.ToStream(scannedImages.Response.FirstOrDefault());
                        Helpers.CleanTempFolder();
                        return File(jpeg, "image/jpeg");
                    case FileTypes.ZIP:
                        var zip = Helpers.CreateZipCollection(scannedImages.Response);
                        Helpers.CleanTempFolder();
                        return File(zip, "application/zip");
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



        //[HttpGet]
        //public ActionResult Scan(
        //    [FromQuery] string? ScannerName,
        //    [FromQuery] FileTypes? FileType, //JPEG,PDF,ZIP
        //    [FromQuery] bool? DuplexEnabled,
        //    [FromQuery] PixelType? Color, //RGB, BlackWhite, Gray
        //    [FromQuery] SupportedSize? PaperSize, //A4,A3...etc
        //    [FromQuery] int? Resolution
        //)
        //{
        //    try
        //    {
        //        NTwain.PlatformInfo.Current.PreferNewDSM = false;
        //        var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
        //        var twainSession = new TwainSession(appId);

        //        twainSession.Open();

        //        List<Image> scannedImages = new List<Image>();
        //        twainSession.DataTransferred += (s, e) =>
        //        {
        //            switch (e.TransferType)
        //            {
        //                case XferMech.Native:
        //                    if (e.NativeData != IntPtr.Zero)
        //                    {
        //                        var stream = e.GetNativeImageStream();
        //                        if (stream != null)
        //                        {
        //                            scannedImages.Add(Image.FromStream(stream));
        //                        }
        //                    }
        //                    break;
        //                case XferMech.File:
        //                    if (String.IsNullOrEmpty(e.FileDataPath))
        //                    {
        //                        var img = new Bitmap(e.FileDataPath);
        //                        scannedImages.Add(img);
        //                    }
        //                    break;
        //                case XferMech.Memory:
        //                    if (e.MemoryData != null && e.MemoryData.Length > 0)
        //                    {
        //                        scannedImages.Add(ToImage(e.MemoryData, e.ImageInfo));
        //                    }
        //                    break;
        //            }

        //        };

        //        var TransferError = false;
        //        twainSession.TransferError += (s, e) =>
        //        {
        //            TransferError = true;
        //        };

        //        DataSource scanner = twainSession.DefaultSource;
        //        if (scanner == null)
        //        {
        //            return StatusCode(StatusCodes.Status406NotAcceptable, new
        //            {
        //                Code = Errors.NoScannerFound,
        //                Message = $"Could not find any scanner"
        //            });
        //        }

        //        if (ScannerName != null)
        //        {
        //            var value = twainSession.GetSources().FirstOrDefault(x => x.Name == ScannerName);
        //            if (value != null)
        //            {
        //                scanner = value;
        //            }
        //            else
        //            {
        //                return StatusCode(StatusCodes.Status406NotAcceptable, new
        //                {
        //                    Code = Errors.ScannerNotAvailable,
        //                    Message = $"'{ScannerName}' scanner is not available"
        //                });
        //            }
        //        }
        //        scanner.Open();
        //        if (!scanner.IsOpen)
        //        {
        //            return StatusCode(StatusCodes.Status406NotAcceptable, new
        //            {
        //                Code = Errors.CouldNotOpenScanner,
        //                Message = $"Could not open the scanner: '{scanner.Name}'"
        //            });
        //        }

        //        if (scanner.Capabilities.ACapXferMech.IsSupported)
        //        {
        //            scanner.Capabilities.ACapXferMech.SetValue(XferMech.Native);
        //            //scanner.Capabilities.ACapXferMech.SetValue(XferMech.Memory);
        //        }
        //        if (scanner.Capabilities.ICapXferMech.IsSupported)
        //        {
        //            scanner.Capabilities.ICapXferMech.SetValue(XferMech.Native);
        //            //scanner.Capabilities.ICapXferMech.SetValue(XferMech.Memory);
        //        }
        //        //if (scanner.Capabilities.CapClearPage.IsSupported)
        //        //{
        //        //    scanner.Capabilities.CapClearPage.SetValue(BoolType.True);
        //        //}
        //        //if (scanner.Capabilities.CapClearBuffers.IsSupported)
        //        //{
        //        //    scanner.Capabilities.CapClearBuffers.SetValue(ClearBuffer.Clear);
        //        //}

        //        if (scanner.Capabilities.ICapAutoDiscardBlankPages.IsSupported)
        //        {
        //            scanner.Capabilities.ICapAutoDiscardBlankPages.SetValue(BlankPage.Auto);
        //        }

        //        if (scanner.Capabilities.CapDuplexEnabled.IsSupported)
        //        {
        //            if (DuplexEnabled == null) DuplexEnabled = true;
        //            var value = (bool)DuplexEnabled ? BoolType.True : BoolType.False;
        //            scanner.Capabilities.CapDuplexEnabled.SetValue(value);
        //        }

        //        if (scanner.Capabilities.ICapPixelType.IsSupported)
        //        {
        //            if (Color == null) Color = PixelType.RGB;
        //            scanner.Capabilities.ICapPixelType.SetValue((PixelType)Color);
        //        }

        //        if (PaperSize != null && scanner.Capabilities.ICapSupportedSizes.IsSupported)
        //        {
        //            scanner.Capabilities.ICapSupportedSizes.SetValue((SupportedSize)PaperSize);
        //        }

        //        if (scanner.Capabilities.ICapXResolution.IsSupported && scanner.Capabilities.ICapYResolution.IsSupported)
        //        {
        //            if (Resolution == null) Resolution = 200;
        //            var value = (TWFix32)Resolution;
        //            scanner.Capabilities.ICapXResolution.SetValue(value);
        //            scanner.Capabilities.ICapYResolution.SetValue(value);
        //        }

        //        // Start Scan
        //        scanner.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero);
        //        //scanner.Enable(SourceEnableMode.ShowUI, false, IntPtr.Zero);

        //        scanner.Close();
        //        twainSession.Close();

        //        if (TransferError)
        //        {
        //            return StatusCode(StatusCodes.Status417ExpectationFailed, new
        //            {
        //                Code = Errors.TransferError,
        //                Message = $"Transfer Error"
        //            });
        //        }

        //        if (scannedImages == null || scannedImages.Count == 0)
        //        {
        //            return StatusCode(StatusCodes.Status406NotAcceptable, new
        //            {
        //                Code = Errors.NoImagesScanned,
        //                Message = $"No images could be scanned"
        //            });
        //        }

        //        switch (FileType)
        //        {
        //            case null:
        //            case FileTypes.PDF:
        //                return File(CreatePdfDocument(scannedImages), "application/pdf");
        //            case FileTypes.JPEG:
        //                return File(ToStream(scannedImages.FirstOrDefault()), "image/jpeg");
        //            case FileTypes.ZIP:
        //                return File(CreateZipCollection(scannedImages), "application/zip");
        //            default:
        //                return StatusCode(StatusCodes.Status406NotAcceptable, new
        //                {
        //                    Code = Errors.InvalidFileType,
        //                    Message = $"Invalid file type: '{FileType}'"
        //                });
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(StatusCodes.Status406NotAcceptable, new
        //        {
        //            Code = Errors.General,
        //            Message = ex.Message + " - " + ex.InnerException?.Message
        //        });
        //    }
        //}



        //[HttpGet]
        //public ActionResult GetAvailableScannersDetailed()
        //{
        //    try
        //    {
        //        NTwain.PlatformInfo.Current.PreferNewDSM = false;
        //        var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
        //        var twainSession = new TwainSession(appId);

        //        twainSession.Open();

        //        var list = new List<dynamic>();

        //        twainSession.GetSources().ToList().ForEach(source =>
        //        {
        //            source.Open();
        //            if (source.IsOpen)
        //            {
        //                var item = new
        //                {
        //                    Name = source.Name,
        //                    SupportDuplex = source.Capabilities.CapDuplexEnabled.IsSupported,
        //                    SupportedColors = source.Capabilities.ICapPixelType?.GetValues()?.Select(x => x.ToString()),
        //                    SupportedPaperSizes = source.Capabilities.ICapSupportedSizes?.GetValues()?.Select(x => x.ToString()),
        //                    SupportedResolutions = source.Capabilities.ICapXResolution?.GetValues()?.Where(dpi => (dpi % 50) == 0).Select(x => x.Whole)
        //                };
        //                list.Add(item);
        //                source.Close();
        //            }
        //        });
        //        twainSession.Close();
        //        return Ok(list);

        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(StatusCodes.Status406NotAcceptable, new
        //        {
        //            Code = Errors.General,
        //            Message = ex.Message + " - " + ex.InnerException?.Message
        //        });
        //    }
        //}



    }
}