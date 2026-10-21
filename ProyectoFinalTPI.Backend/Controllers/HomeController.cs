using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Models;

namespace ProyectoFinalTPI.Backend.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [SwaggerOperation(
            Summary = "Muestra la página de inicio.",
            Description = "Devuelve la vista HTML de inicio de la aplicación."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Vista HTML renderizada.")]
        public IActionResult Index()
        {
            return View();
        }

        [SwaggerOperation(
            Summary = "Muestra la página de privacidad.",
            Description = "Devuelve la vista HTML de privacidad de la aplicación."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Vista HTML renderizada.")]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [SwaggerOperation(
            Summary = "Muestra la vista de error.",
            Description = "Devuelve la vista HTML de error con el identificador de la solicitud. Deshabilita el almacenamiento en caché."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Vista HTML renderizada.")]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
