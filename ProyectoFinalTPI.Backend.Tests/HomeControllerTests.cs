using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Models;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class HomeControllerTests
{
    private readonly Mock<ILogger<HomeController>> _loggerMock = new();

    #region Vistas Principales

    [Fact]
    public void QueIndexDevuelvaViewResult()
    {
        //Arrange
        var controller = new HomeController(_loggerMock.Object);

        //Act
        var resultado = controller.Index();

        //Assert
        Assert.IsType<ViewResult>(resultado);
    }

    [Fact]
    public void QuePrivacyDevuelvaViewResult()
    {
        //Arrange
        var controller = new HomeController(_loggerMock.Object);

        //Act
        var resultado = controller.Privacy();

        //Assert
        Assert.IsType<ViewResult>(resultado);
    }

    [Fact]
    public void QueErrorDevuelvaViewResultConErrorViewModel()
    {
        //Arrange
        var controller = new HomeController(_loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-test-123" }
            }
        };

        //Act
        var resultado = controller.Error();

        //Assert
        var viewResult = Assert.IsType<ViewResult>(resultado);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal("trace-test-123", model.RequestId);
        Assert.True(model.ShowRequestId);
    }

    #endregion
}

