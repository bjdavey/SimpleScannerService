using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saraff.Twain;
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
                List<Source> list = new List<Source>();
                string exceptionAuxX86 = "";
                string exceptionAuxMSIL = "";
                TwainExternalProcess.Execute(Helpers.AuxX86Path(),
                    twain =>
                    {
                        try
                        {
                            for (var i = 0; i < twain.SourcesCount; i++)
                            {
                                list.Add(new Source
                                {
                                    Id = i,
                                    Name = twain.GetSourceProductName(i),
                                    Platform = "x32",
                                    TwainVersion = twain.IsTwain2Supported ? 2 : 1,
                                    IsDefault = twain.SourceIndex == i
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptionAuxX86 += ex.Message + " - " + ex.InnerException?.Message;
                        }
                    });
                TwainExternalProcess.Execute(Helpers.AuxMSILPath(),
                    twain =>
                    {
                        try
                        {
                            if (!twain.IsTwain2Supported)
                            {
                                return;
                            }
                            for (var i = 0; i < twain.SourcesCount; i++)
                            {
                                list.Add(new Source
                                {
                                    Id = i,
                                    Name = twain.GetSourceProductName(i),
                                    Platform = Environment.Is64BitOperatingSystem ? "x64" : "x32",
                                    TwainVersion = twain.IsTwain2Supported ? 2 : 1,
                                    IsDefault = twain.SourceIndex == i
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptionAuxMSIL += ex.Message + " - " + ex.InnerException?.Message;
                        }
                    });

                var auxException = "";

                if (!string.IsNullOrEmpty(exceptionAuxX86))
                    auxException = $"AuxX86 Exception: {exceptionAuxX86}";

                if (!string.IsNullOrEmpty(exceptionAuxMSIL))
                {
                    if (string.IsNullOrEmpty(auxException))
                        auxException += $"AuxMSIL Exception: {exceptionAuxMSIL}";
                    else
                        auxException += $" + AuxMSIL Exception: {exceptionAuxMSIL}";
                }

                if (!string.IsNullOrEmpty(auxException))
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.General,
                        Message = auxException
                    });
                }

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
                SourceDetails? sourceDetails = null;

                string exceptionAuxMSIL = "";
                string exceptionAuxX86 = "";
                bool exceptionOpenScanner = false;

                //Firstly, let's try AuxMSIL
                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem)
                {
                    TwainExternalProcess.Execute(Helpers.AuxMSILPath(),
                        twain =>
                        {
                            try
                            {
                                if (!twain.IsTwain2Supported)
                                {
                                    return;
                                }
                                sourceDetails = Helpers.GetSourceDetails(twain, ScannerName.Trim());
                            }
                            catch (Saraff.Twain.TwainException ex)
                            {
                                if (ex.ConditionCode == Saraff.Twain.TwCC.CheckDeviceOnline)
                                    exceptionOpenScanner = true;
                                else
                                    exceptionAuxMSIL += ex.Message + " - " + ex.InnerException?.Message;
                            }
                            catch (Exception ex)
                            {
                                exceptionAuxMSIL += ex.Message + " - " + ex.InnerException?.Message;
                            }
                        });
                }
                #endregion

                if (sourceDetails != null)
                    return Ok(sourceDetails);
                if (exceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                //Let's try AuxX86Path if we couldn't find the source
                #region AuxX86
                TwainExternalProcess.Execute(Helpers.AuxX86Path(),
                        twain =>
                        {
                            try
                            {
                                sourceDetails = Helpers.GetSourceDetails(twain, ScannerName.Trim());
                            }
                            catch (Saraff.Twain.TwainException ex)
                            {
                                if (ex.ConditionCode == Saraff.Twain.TwCC.CheckDeviceOnline)
                                    exceptionOpenScanner = true;
                                else
                                    exceptionAuxX86 += ex.Message + " - " + ex.InnerException?.Message;
                            }
                            catch (Exception ex)
                            {
                                exceptionAuxX86 += ex.Message + " - " + ex.InnerException?.Message;
                            }
                        });
                #endregion

                if (sourceDetails != null)
                    return Ok(sourceDetails);
                if (exceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }


                var auxException = "";
                if (!string.IsNullOrEmpty(exceptionAuxX86))
                    auxException = $"AuxX86 Exception: {exceptionAuxX86}";
                if (!string.IsNullOrEmpty(exceptionAuxMSIL))
                {
                    if (string.IsNullOrEmpty(auxException))
                        auxException += $"AuxMSIL Exception: {exceptionAuxMSIL}";
                    else
                        auxException += $" + AuxMSIL Exception: {exceptionAuxMSIL}";
                }
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.ScannerNotAvailable,
                    Message = $"'{ScannerName}' scanner is not available"
                });
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
            [FromQuery] int? Resolution
        )
        {
            try
            {
                bool sourceFound = false;

                string exceptionAuxMSIL = "";
                string exceptionAuxX86 = "";
                bool exceptionOpenScanner = false;

                List<Image> scannedImages = new List<Image>();

                //Firstly, let's try AuxMSIL
                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem)
                {
                    TwainExternalProcess.Execute(Helpers.AuxMSILPath(),
                        twain =>
                        {
                            try
                            {
                                if (!twain.IsTwain2Supported)
                                {
                                    return;
                                }
                                for (var i = 0; i < twain.SourcesCount; i++)
                                {
                                    if (ScannerName.Trim() == twain.GetSourceProductName(i))
                                    {
                                        twain.SourceIndex = i;
                                        twain.OpenDataSource();
                                        sourceFound = true;

                                        twain.SetupMemXferEvent += (sender, e) =>
                                        {
                                            if (twain.Capabilities.XferMech.GetCurrent() == TwSX.Memory)
                                            {
                                                if (TiffMemXfer.Current == null)
                                                {
                                                    TiffMemXfer.Create((int)e.BufferSize);
                                                }
                                                if (e.ImageInfo.PixelType == TwPixelType.Palette)
                                                {
                                                    Twain32.ColorPalette _palette = twain.Palette.Get();
                                                    TiffMemXfer.Current.ColorMap = new ushort[_palette.Colors.Length * 3];
                                                    for (int i = 0; i < _palette.Colors.Length; i++)
                                                    {
                                                        TiffMemXfer.Current.ColorMap[i] = (ushort)(_palette.Colors[i].R);
                                                        TiffMemXfer.Current.ColorMap[i + _palette.Colors.Length] = (ushort)(_palette.Colors[i].G);
                                                        TiffMemXfer.Current.ColorMap[i + (_palette.Colors.Length << 1)] = (ushort)(_palette.Colors[i].B);
                                                    }
                                                }
                                            }
                                            else
                                            { // MemFile
                                                MemFileXfer.Create((int)e.BufferSize, twain.Capabilities.ImageFileFormat.GetCurrent().ToString().ToLower());
                                            }
                                        };
                                        twain.MemXferEvent += new System.EventHandler<Saraff.Twain.Twain32.MemXferEventArgs>(Helpers._twain32_MemXferEvent);

                                        twain.EndXfer += (sender, e) =>
                                        {
                                            try
                                            {
                                                using (var image = e.Image)
                                                {
                                                    if (image != null)
                                                    {
                                                        Bitmap deepCopy = new Bitmap(image);
                                                        scannedImages.Add(deepCopy);
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                            }
                                        };
                                        twain.Acquire();
                                        break;
                                    }
                                }
                            }
                            catch (Saraff.Twain.TwainException ex)
                            {
                                if (ex.ConditionCode == Saraff.Twain.TwCC.CheckDeviceOnline)
                                    exceptionOpenScanner = true;
                                else
                                    exceptionAuxMSIL += ex.Message + " - " + ex.InnerException?.Message;
                            }
                            catch (Exception ex)
                            {
                                exceptionAuxMSIL += ex.Message + " - " + ex.InnerException?.Message;
                            }
                        });
                }
                #endregion

                if (sourceFound)
                    return File(Helpers.ToStream(scannedImages.FirstOrDefault()), "image/jpeg");
                if (exceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                //Let's try AuxX86Path if we couldn't find the source
                #region AuxX86
                TwainExternalProcess.Execute(Helpers.AuxX86Path(),
                        twain =>
                        {
                            try
                            {
                                for (var i = 0; i < twain.SourcesCount; i++)
                                {
                                    if (ScannerName.Trim() == twain.GetSourceProductName(i))
                                    {
                                        twain.SourceIndex = i;
                                        twain.OpenDataSource();
                                        sourceFound = true;

                                        twain.SetupMemXferEvent += (sender, e) =>
                                        {
                                            if (twain.Capabilities.XferMech.GetCurrent() == TwSX.Memory)
                                            {
                                                if (TiffMemXfer.Current == null)
                                                {
                                                    TiffMemXfer.Create((int)e.BufferSize);
                                                }
                                                if (e.ImageInfo.PixelType == TwPixelType.Palette)
                                                {
                                                    Twain32.ColorPalette _palette = twain.Palette.Get();
                                                    TiffMemXfer.Current.ColorMap = new ushort[_palette.Colors.Length * 3];
                                                    for (int i = 0; i < _palette.Colors.Length; i++)
                                                    {
                                                        TiffMemXfer.Current.ColorMap[i] = (ushort)(_palette.Colors[i].R);
                                                        TiffMemXfer.Current.ColorMap[i + _palette.Colors.Length] = (ushort)(_palette.Colors[i].G);
                                                        TiffMemXfer.Current.ColorMap[i + (_palette.Colors.Length << 1)] = (ushort)(_palette.Colors[i].B);
                                                    }
                                                }
                                            }
                                            else
                                            { // MemFile
                                                MemFileXfer.Create((int)e.BufferSize, twain.Capabilities.ImageFileFormat.GetCurrent().ToString().ToLower());
                                            }
                                        };
                                        twain.MemXferEvent += new System.EventHandler<Saraff.Twain.Twain32.MemXferEventArgs>(Helpers._twain32_MemXferEvent);

                                        twain.EndXfer += (sender, e) =>
                                        {
                                            try
                                            {
                                                using (var image = e.Image)
                                                {
                                                    if (image != null)
                                                    {
                                                        Bitmap deepCopy = new Bitmap(image);
                                                        scannedImages.Add(deepCopy);
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                            }
                                        };
                                        twain.Acquire();
                                        break;
                                    }
                                }
                            }
                            catch (Saraff.Twain.TwainException ex)
                            {
                                if (ex.ConditionCode == Saraff.Twain.TwCC.CheckDeviceOnline)
                                    exceptionOpenScanner = true;
                                else
                                    exceptionAuxX86 += ex.Message + " - " + ex.InnerException?.Message;
                            }
                            catch (Exception ex)
                            {
                                exceptionAuxX86 += ex.Message + " - " + ex.InnerException?.Message;
                            }
                        });
                #endregion

                if (sourceFound)
                    return File(Helpers.ToStream(scannedImages.FirstOrDefault()), "image/jpeg");
                if (exceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                var auxException = "";
                if (!string.IsNullOrEmpty(exceptionAuxX86))
                    auxException = $"AuxX86 Exception: {exceptionAuxX86}";
                if (!string.IsNullOrEmpty(exceptionAuxMSIL))
                {
                    if (string.IsNullOrEmpty(auxException))
                        auxException += $"AuxMSIL Exception: {exceptionAuxMSIL}";
                    else
                        auxException += $" + AuxMSIL Exception: {exceptionAuxMSIL}";
                }
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.ScannerNotAvailable,
                    Message = $"'{ScannerName}' scanner is not available"
                });
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