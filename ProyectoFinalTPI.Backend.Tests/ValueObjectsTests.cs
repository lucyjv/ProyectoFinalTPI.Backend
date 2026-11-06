using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using NetTopologySuite.Geometries;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AñoTests
{
    #region Creación y Validación de Año

    [Theory]
    [InlineData(1910)]
    [InlineData(1987)]
    [InlineData(2000)]
    public void QueAnioValidoSeCreeCorrectamente(int valor)
    {
        //Arrange & Act
        var año = new Año(valor);

        //Assert
        Assert.Equal(valor, año.Valor);
    }

    [Theory]
    [InlineData(1908)]
    [InlineData(0)]
    [InlineData(-100)]
    public void QueAnioMenorA1900LanceExcepcion(int valor)
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Año(valor));
    }

    [Fact]
    public void QueAnioFuturoLanceExcepcion()
    {
        //Arrange
        var añoFuturo = DateTime.UtcNow.Year + 1;

        //Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Año(añoFuturo));
    }

    #endregion

    #region Conversión e Igualdad de Año

    [Fact]
    public void QueDesdeFechaExtraigaElAnioCorrectamente()
    {
        //Arrange
        var fecha = new DateTime(1987, 3, 15);

        //Act
        var año = Año.DesdeFecha(fecha);

        //Assert
        Assert.Equal(1987, año.Valor);
    }

    [Fact]
    public void QueADecadaDevuelvaLaDecadaCorrecta()
    {
        //Arrange
        var año = new Año(1987);

        //Act
        var decada = año.ADecada();

        //Assert
        Assert.Equal(1980, decada.Valor);
    }

    [Fact]
    public void QueDosAniosConMismoValorSeanIguales()
    {
        //Arrange, Act & Assert
        Assert.Equal(new Año(1990), new Año(1990));
    }

    [Fact]
    public void QueDosAniosConDistintoValorNoSeanIguales()
    {
        //Arrange, Act & Assert
        Assert.NotEqual(new Año(1990), new Año(1991));
    }

    #endregion
}

public class DecadaTests
{
    #region Creación y Validación de Década

    [Theory]
    [InlineData(1980, "80s")]
    [InlineData(1990, "90s")]
    [InlineData(2000, "2000s")]
    [InlineData(2010, "10s")]
    public void QueEtiquetaDevuelvaElTextoCorrecto(int valor, string etiquetaEsperada)
    {
        //Arrange
        var decada = new Decada(valor);

        //Act
        var etiqueta = decada.Etiqueta();

        //Assert
        Assert.Equal(etiquetaEsperada, etiqueta);
    }

    [Theory]
    [InlineData(1983)]
    [InlineData(1995)]
    [InlineData(2007)]
    public void QueValorNoMultiploDe10LanceExcepcion(int valor)
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Decada(valor));
    }

    [Fact]
    public void QueDeAnioInfieraLaDecadaCorrectamente()
    {
        //Arrange & Act
        var decada = Decada.DeAño(1987);

        //Assert
        Assert.Equal(1980, decada.Valor);
    }

    [Fact]
    public void QueDeAnioConInicioDeDecadaSeaLaMismaDecada()
    {
        //Arrange & Act
        var decada = Decada.DeAño(1980);

        //Assert
        Assert.Equal(1980, decada.Valor);
    }

    [Fact]
    public void QueDosDecadasConMismoValorSeanIguales()
    {
        //Arrange, Act & Assert
        Assert.Equal(new Decada(1990), new Decada(1990));
    }

    #endregion
}

public class CoordenadaTests
{
    #region Creación y Validación de Coordenadas

    [Theory]
    [InlineData(-34.6037, -58.3816)]  // Buenos Aires
    [InlineData(90.0, 180.0)]         // límites máximos
    [InlineData(-90.0, -180.0)]       // límites mínimos
    [InlineData(0.0, 0.0)]            // punto nulo geográfico
    public void QueCoordenadaValidaSeCreeCorrectamente(double lat, double lon)
    {
        //Arrange & Act
        var coord = new Coordenada(lat, lon);

        //Assert
        Assert.Equal(lat, coord.Latitud);
        Assert.Equal(lon, coord.Longitud);
    }

    [Theory]
    [InlineData(90.1, 0.0)]
    [InlineData(-90.1, 0.0)]
    public void QueLatitudFueraDeRangoLanceExcepcion(double lat, double lon)
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coordenada(lat, lon));
    }

    [Theory]
    [InlineData(0.0, 180.1)]
    [InlineData(0.0, -180.1)]
    public void QueLongitudFueraDeRangoLanceExcepcion(double lat, double lon)
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coordenada(lat, lon));
    }

    #endregion

    #region Conversión con NetTopologySuite

    [Fact]
    public void QueAPointGenerePuntoNtsConValoresCorrectos()
    {
        //Arrange
        var coord = new Coordenada(-34.6037, -58.3816);

        //Act
        var point = coord.APoint();

        //Assert (En NTS: X = Longitud, Y = Latitud)
        Assert.Equal(-58.3816, point.X);
        Assert.Equal(-34.6037, point.Y);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public void QueDesdePuntoReconstruyaCoordenadaCorrectamente()
    {
        //Arrange
        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var point = factory.CreatePoint(new Coordinate(-58.3816, -34.6037));

        //Act
        var coord = Coordenada.DesdePunto(point);

        //Assert
        Assert.Equal(-34.6037, coord.Latitud);
        Assert.Equal(-58.3816, coord.Longitud);
    }

    [Fact]
    public void QueDesdePuntoConPuntoVacioLanceExcepcion()
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => Coordenada.DesdePunto(Point.Empty));
    }

    [Fact]
    public void QueDosCoordenadasConMismosValoresSeanIguales()
    {
        //Arrange, Act & Assert
        Assert.Equal(new Coordenada(-34.6037, -58.3816), new Coordenada(-34.6037, -58.3816));
    }

    #endregion
}

public class FechaEfemérideTests
{
    #region Creación y Validación de Efemérides

    [Theory]
    [InlineData(5, 25)]   // 25 de mayo
    [InlineData(7, 9)]    // 9 de julio
    [InlineData(2, 29)]   // 29 de febrero
    public void QueFechaEfemerideValidaSeCreeCorrectamente(int mes, int dia)
    {
        //Arrange & Act
        var efemeride = new FechaEfemeride(mes, dia);

        //Assert
        Assert.Equal(mes, efemeride.Mes);
        Assert.Equal(dia, efemeride.Dia);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(13, 1)]
    public void QueMesInvalidoLanceExcepcion(int mes, int dia)
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new FechaEfemeride(mes, dia));
    }

    [Theory]
    [InlineData(1, 32)]
    [InlineData(4, 31)]
    [InlineData(2, 30)]
    public void QueDiaInvalidoParaElMesLanceExcepcion(int mes, int dia)
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new FechaEfemeride(mes, dia));
    }

    #endregion

    #region Coincidencia y Próxima Ocurrencia

    [Fact]
    public void QueDesdeFechaIgnoreElAnio()
    {
        //Arrange & Act
        var efemeride = FechaEfemeride.DesdeFecha(new DateTime(1810, 5, 25));

        //Assert
        Assert.Equal(5, efemeride.Mes);
        Assert.Equal(25, efemeride.Dia);
    }

    [Fact]
    public void QueCoincideDevuelvaTrueCuandoMesYDiaCoinciden()
    {
        //Arrange
        var efemeride = new FechaEfemeride(5, 25);

        //Act & Assert
        Assert.True(efemeride.Coincide(new DateTime(2024, 5, 25)));
        Assert.True(efemeride.Coincide(new DateTime(1810, 5, 25)));
    }

    [Fact]
    public void QueCoincideDevuelvaFalseCuandoNoCoincide()
    {
        //Arrange
        var efemeride = new FechaEfemeride(5, 25);

        //Act & Assert
        Assert.False(efemeride.Coincide(new DateTime(2024, 5, 26)));
        Assert.False(efemeride.Coincide(new DateTime(2024, 7, 9)));
    }

    [Fact]
    public void QueProximaOcurrenciaDevuelvaFechaFutura()
    {
        //Arrange & Act
        var efemeride = FechaEfemeride.DesdeFecha(DateTime.UtcNow.AddDays(10));

        //Assert
        Assert.True(efemeride.ProximaOcurrencia() >= DateTime.UtcNow.Date);
    }

    #endregion
}

public class NivelCategoriaTests
{
    #region Cálculo y Niveles

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(19, 2)]
    [InlineData(20, 3)]
    [InlineData(50, 4)]
    [InlineData(100, 5)]
    [InlineData(999, 5)]
    public void QueCalcularParaDevuelvaNivelCorrecto(int publicaciones, int nivelEsperado)
    {
        //Arrange & Act
        var nivel = NivelCategoria.CalcularPara(publicaciones);

        //Assert
        Assert.Equal(nivelEsperado, nivel.Numero);
    }

    [Fact]
    public void QueCalcularParaConNegativoLanceExcepcion()
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => NivelCategoria.CalcularPara(-1));
    }

    [Fact]
    public void QueSiguienteNivelDevuelvaNullEnNivelMaximo()
    {
        //Arrange
        var nivelMax = NivelCategoria.CalcularPara(100);

        //Act
        var siguiente = nivelMax.SiguienteNivel();

        //Assert
        Assert.Null(siguiente);
    }

    [Fact]
    public void QueSiguienteNivelDevuelvaElNivelCorrecto()
    {
        //Arrange
        var nivel1 = NivelCategoria.CalcularPara(0);

        //Act
        var siguiente = nivel1.SiguienteNivel();

        //Assert
        Assert.NotNull(siguiente);
        Assert.Equal(2, siguiente.Numero);
    }

    [Fact]
    public void QuePublicacionesParaSubirCalculeCorrectamente()
    {
        //Arrange
        var nivel = NivelCategoria.CalcularPara(0);

        //Act & Assert
        Assert.Equal(4, nivel.PublicacionesParaSubir(1));
        Assert.Equal(0, nivel.PublicacionesParaSubir(5));
    }

    [Fact]
    public void QueTodosDevuelvaCincoNiveles()
    {
        //Arrange & Act
        var todos = NivelCategoria.Todos();

        //Assert
        Assert.Equal(5, todos.Count);
    }

    #endregion
}

public class LimiteDiarioTests
{
    #region Propiedades de Límites Estándar y Premium

    [Fact]
    public void QueEstandarTengaLimiteDe10()
    {
        //Arrange & Act & Assert
        Assert.Equal(10, LimiteDiario.Estandar.Maximo);
    }

    [Fact]
    public void QuePremiumTengaLimiteDe30()
    {
        //Arrange & Act & Assert
        Assert.Equal(30, LimiteDiario.Premium.Maximo);
    }

    [Fact]
    public void QueIlimitadoNoEsteAlcanzadoNunca()
    {
        //Arrange, Act & Assert
        Assert.False(LimiteDiario.Ilimitado.AlcanzadoPor(999999));
        Assert.True(LimiteDiario.Ilimitado.EsIlimitado);
    }

    #endregion

    #region Verificación de Límites y Restantes

    [Fact]
    public void QueAlcanzadoPorDevuelvaTrueCuandoLlegaAlMaximo()
    {
        //Arrange
        var limite = LimiteDiario.Estandar;

        //Act & Assert
        Assert.True(limite.AlcanzadoPor(10));
        Assert.True(limite.AlcanzadoPor(11));
    }

    [Fact]
    public void QueAlcanzadoPorDevuelvaFalseCuandoNoLlegaAlMaximo()
    {
        //Arrange
        var limite = LimiteDiario.Estandar;

        //Act & Assert
        Assert.False(limite.AlcanzadoPor(9));
        Assert.False(limite.AlcanzadoPor(0));
    }

    [Fact]
    public void QueRestantesParaCalculeCorrectamente()
    {
        //Arrange
        var limite = LimiteDiario.Estandar;

        //Act & Assert
        Assert.Equal(7, limite.RestantesPara(3));
        Assert.Equal(0, limite.RestantesPara(10));
        Assert.Equal(0, limite.RestantesPara(15));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    public void QueParaNivelAsigneLimiteCorrecto(int numeroNivel, bool esPremium)
    {
        //Arrange
        var nivel = NivelCategoria.CalcularPara(numeroNivel switch
        {
            1 => 0, 2 => 5, 3 => 20, 4 => 50, 5 => 100, _ => 0
        });

        //Act
        var limite = LimiteDiario.ParaNivel(nivel);

        //Assert
        Assert.Equal(esPremium ? LimiteDiario.Premium : LimiteDiario.Estandar, limite);
    }

    [Fact]
    public void QueLimiteConMaximoCeroONegativoLanceExcepcion()
    {
        //Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new LimiteDiario(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LimiteDiario(-5));
    }

    #endregion
}
