using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace PugetSound.Controllers
{
    [AllowAnonymous]
    public class ExternalController : Controller
    {
        private readonly ILogger<ExternalController> _logger;

        public ExternalController(ILogger<ExternalController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View("Index");
        }

        [Route("howitworks")]
        public IActionResult HowItWorks()
        {
            return View("HowItWorks");
        }
    }
}
