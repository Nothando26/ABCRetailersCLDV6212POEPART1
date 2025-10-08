using ABCRetailersPOEPART1.Models;
using ABCRetailersPOEPART1.Models;
using ABCRetailersPOEPART1.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailersPOEPART1.Controllers
{
    public class UploadController : Controller
    {
        private readonly IFunctionsApi _api;

        public UploadController(IFunctionsApi api)
        {
            _api = api;
        }

        public IActionResult Index()
        {
            return View(new FileUploadModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (model.ProofOfPayment != null && model.ProofOfPayment.Length > 0)
                    {
                        // Upload the proof of payment
                        var fileName = await _api.UploadProofOfPaymentAsync(
                            model.ProofOfPayment,
                            model.OrderId,
                            model.CustomerName
                        );

                        TempData["Success"] = $"File uploaded successfully! File name: {fileName}";

                        // Clear the model for a fresh form
                        return View(new FileUploadModel());
                    }
                    else
                    {
                        ModelState.AddModelError("ProofOfPayment", "Please select a file to upload.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                }
            }

            return View(model);
        }
    }
}
