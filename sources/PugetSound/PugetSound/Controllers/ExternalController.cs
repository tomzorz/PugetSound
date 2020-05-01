using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            _logger.Log(LogLevel.Information, "Loaded welcome page.");
            return View("Index");
        }

        [Route("howitworks")]
        public IActionResult HowItWorks()
        {
            _logger.Log(LogLevel.Information, "Loaded how it works page.");
            return View("HowItWorks");
        }
    }
}
