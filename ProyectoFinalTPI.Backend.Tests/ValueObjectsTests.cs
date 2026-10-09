using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using NetTopologySuite.Geometries;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AñoTests
{
    [Theory]
    [InlineData(1910)]
    [InlineData(1987)]
    [InlineData(2000)]
    public void Año_valido_se_crea_correctamente(int valor)
    {
        var año = new Año(valor);
        Assert.Equal(valor, año.Valor);
    }

    [Theory]
    [InlineData(1908)]
    [InlineData(0)]
    [InlineData(-100)]
    public void Año_menor_a_1900_lanza_excepcion(int valor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Año(valor));
    }

    [Fact]
    public void Año_futuro_lanza_excepcion()
    {
        var añoFuturo = DateTime.UtcNow.Year + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => new Año(añoFuturo));
    }

    [Fact]
    public void DesdeFecha_extrae_el_año_correctamente()
    {
        var fecha = new DateTime(1987, 3, 15);
        var año = Año.DesdeFecha(fecha);
        Assert.Equal(1987, año.Valor);
    }

    [Fact]
    public void ADecada_devuelve_la_decada_correcta()
    {
        var año = new Año(1987);
        var decada = año.ADecada();
        Assert.Equal(1980, decada.Valor);
    }

    [Fact]
    public void Dos_años_con_mismo_valor_son_iguales()
    {
        Assert.Equal(new Año(1990), new Año(1990));
    }

    [Fact]
    public void Dos_años_con_distinto_valor_no_son_iguales()
    {
        Assert.NotEqual(new Año(1990), new Año(1991));
    }
}

public class DecadaTests
{
    [Theory]
    [InlineData(1980, "80s")]
    [InlineData(1990, "90s")]
    [InlineData(2000, "2000s")]
    [InlineData(2010, "10s")]
    public void Etiqueta_devuelve_el_texto_correcto(int valor, string etiquetaEsperada)
    {
        var decada = new Decada(valor);
        Assert.Equal(etiquetaEsperada, decada.Etiqueta());
    }

    [Theory]
    [InlineData(1983)]
    [InlineData(1995)]
    [InlineData(2007)]
    public void Valor_no_multiplo_de_10_lanza_excepcion(int valor)
    {
        Assert.Throws<ArgumentException>(() => new Decada(valor));
    }

    [Fact]
    public void DeAño_infiere_la_decada_correctamente()
    {
        var decada = Decada.DeAño(1987);
        Assert.Equal(1980, decada.Valor);
    }

    [Fact]
    public void DeAño_con_inicio_de_decada_es_la_misma_decada()
    {
        var decada = Decada.DeAño(1980);
        Assert.Equal(1980, decada.Valor);
    }

    [Fact]
    public void Dos_decadas_con_mismo_valor_son_iguales()
    {
        Assert.Equal(new Decada(1990), new Decada(1990));
    }
}

public class CoordenadaTests
{
    [Theory]
    [InlineData(-34.6037, -58.3816)]  // Buenos Aires
    [InlineData(90.0, 180.0)]         // límites máximos
    [InlineData(-90.0, -180.0)]       // límites mínimos
    [InlineData(0.0, 0.0)]            // punto nulo geográfico
    public void Coordenada_valida_se_crea_correctamente(double lat, double lon)
    {
        var coord = new Coordenada(lat, lon);
        Assert.Equal(lat, coord.Latitud);
        Assert.Equal(lon, coord.Longitud);
    }

    [Theory]
    [InlineData(90.1, 0.0)]
    [InlineData(-90.1, 0.0)]
    public void Latitud_fuera_de_rango_lanza_excepcion(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coordenada(lat, lon));
    }

    [Theory]
    [InlineData(0.0, 180.1)]
    [InlineData(0.0, -180.1)]
    public void Longitud_fuera_de_rango_lanza_excepcion(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coordenada(lat, lon));
    }

    [Fact]
    public void APoint_genera_punto_NTS_con_valores_correctos()
    {
        var coord = new Coordenada(-34.6037, -58.3816);
        var point = coord.APoint();

        // En NTS: X = Longitud, Y = Latitud
        Assert.Equal(-58.3816, point.X);
        Assert.Equal(-34.6037, point.Y);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public void DesdePunto_reconstruye_coordenada_correctamente()
    {
        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var point = factory.CreatePoint(new Coordinate(-58.3816, -34.6037));

        var coord = Coordenada.DesdePunto(point);

        Assert.Equal(-34.6037, coord.Latitud);
        Assert.Equal(-58.3816, coord.Longitud);
    }

    [Fact]
    public void DesdePunto_con_punto_vacio_lanza_excepcion()
    {
        Assert.Throws<ArgumentException>(() => Coordenada.DesdePunto(Point.Empty));
    }

    [Fact]
    public void Dos_coordenadas_con_mismos_valores_son_iguales()
    {
        Assert.Equal(new Coordenada(-34.6037, -58.3816), new Coordenada(-34.6037, -58.3816));
    }
}

public class FechaEfemérideTests
{
    [Theory]
    [InlineData(5, 25)]   // 25 de mayo
    [InlineData(7, 9)]    // 9 de julio
    [InlineData(2, 29)]   // 29 de febrero (válido como efeméride)
    public void FechaEfemeride_valida_se_crea_correctamente(int mes, int dia)
    {
        var efemeride = new FechaEfemeride(mes, dia);
        Assert.Equal(mes, efemeride.Mes);
        Assert.Equal(dia, efemeride.Dia);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(13, 1)]
    public void Mes_invalido_lanza_excepcion(int mes, int dia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FechaEfemeride(mes, dia));
    }

    [Theory]
    [InlineData(1, 32)]
    [InlineData(4, 31)]   // abril solo tiene 30 días
    [InlineData(2, 30)]   // febrero no tiene 30
    public void Dia_invalido_para_el_mes_lanza_excepcion(int mes, int dia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FechaEfemeride(mes, dia));
    }

    [Fact]
    public void DesdeFecha_ignora_el_año()
    {
        var efemeride = FechaEfemeride.DesdeFecha(new DateTime(1810, 5, 25));
        Assert.Equal(5, efemeride.Mes);
        Assert.Equal(25, efemeride.Dia);
    }

    [Fact]
    public void Coincide_devuelve_true_cuando_mes_y_dia_coinciden()
    {
        var efemeride = new FechaEfemeride(5, 25);
        Assert.True(efemeride.Coincide(new DateTime(2024, 5, 25)));
        Assert.True(efemeride.Coincide(new DateTime(1810, 5, 25)));
    }

    [Fact]
    public void Coincide_devuelve_false_cuando_no_coincide()
    {
        var efemeride = new FechaEfemeride(5, 25);
        Assert.False(efemeride.Coincide(new DateTime(2024, 5, 26)));
        Assert.False(efemeride.Coincide(new DateTime(2024, 7, 9)));
    }

    [Fact]
    public void ProximaOcurrencia_devuelve_fecha_futura()
    {
        var efemeride = FechaEfemeride.DesdeFecha(DateTime.UtcNow.AddDays(10));
        Assert.True(efemeride.ProximaOcurrencia() >= DateTime.UtcNow.Date);
    }
}

public class NivelCategoriaTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(19, 2)]
    [InlineData(20, 3)]
    [InlineData(50, 4)]
    [InlineData(100, 5)]
    [InlineData(999, 5)]
    public void CalcularPara_devuelve_nivel_correcto(int publicaciones, int nivelEsperado)
    {
        var nivel = NivelCategoria.CalcularPara(publicaciones);
        Assert.Equal(nivelEsperado, nivel.Numero);
    }

    [Fact]
    public void CalcularPara_con_negativo_lanza_excepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NivelCategoria.CalcularPara(-1));
    }

    [Fact]
    public void SiguienteNivel_devuelve_null_en_nivel_maximo()
    {
        var nivelMax = NivelCategoria.CalcularPara(100);
        Assert.Null(nivelMax.SiguienteNivel());
    }

    [Fact]
    public void SiguienteNivel_devuelve_el_nivel_correcto()
    {
        var nivel1 = NivelCategoria.CalcularPara(0);
        var siguiente = nivel1.SiguienteNivel();
        Assert.NotNull(siguiente);
        Assert.Equal(2, siguiente.Numero);
    }

    [Fact]
    public void PublicacionesParaSubir_calcula_correctamente()
    {
        var nivel = NivelCategoria.CalcularPara(0); // nivel 1, necesita 5 para subir
        Assert.Equal(4, nivel.PublicacionesParaSubir(1));
        Assert.Equal(0, nivel.PublicacionesParaSubir(5)); // ya alcanzó
    }

    [Fact]
    public void Todos_devuelve_cinco_niveles()
    {
        Assert.Equal(5, NivelCategoria.Todos().Count);
    }
}

public class LimiteDiarioTests
{
    [Fact]
    public void Estandar_tiene_limite_de_10()
    {
        Assert.Equal(10, LimiteDiario.Estandar.Maximo);
    }

    [Fact]
    public void Premium_tiene_limite_de_30()
    {
        Assert.Equal(30, LimiteDiario.Premium.Maximo);
    }

    [Fact]
    public void Ilimitado_no_esta_alcanzado_nunca()
    {
        Assert.False(LimiteDiario.Ilimitado.AlcanzadoPor(999999));
        Assert.True(LimiteDiario.Ilimitado.EsIlimitado);
    }

    [Fact]
    public void AlcanzadoPor_devuelve_true_cuando_llega_al_maximo()
    {
        var limite = LimiteDiario.Estandar;
        Assert.True(limite.AlcanzadoPor(10));
        Assert.True(limite.AlcanzadoPor(11));
    }

    [Fact]
    public void AlcanzadoPor_devuelve_false_cuando_no_llego_al_maximo()
    {
        var limite = LimiteDiario.Estandar;
        Assert.False(limite.AlcanzadoPor(9));
        Assert.False(limite.AlcanzadoPor(0));
    }

    [Fact]
    public void RestantesPara_calcula_correctamente()
    {
        var limite = LimiteDiario.Estandar; // máximo 10
        Assert.Equal(7, limite.RestantesPara(3));
        Assert.Equal(0, limite.RestantesPara(10));
        Assert.Equal(0, limite.RestantesPara(15)); // no da negativo
    }

    [Theory]
    [InlineData(1, false)]  // nivel 1 → Estándar
    [InlineData(3, false)]  // nivel 3 → Estándar
    [InlineData(4, true)]   // nivel 4 → Premium
    [InlineData(5, true)]   // nivel 5 → Premium
    public void ParaNivel_asigna_limite_correcto(int numeroNivel, bool esPremium)
    {
        var nivel = NivelCategoria.CalcularPara(numeroNivel switch
        {
            1 => 0, 2 => 5, 3 => 20, 4 => 50, 5 => 100, _ => 0
        });
        var limite = LimiteDiario.ParaNivel(nivel);
        Assert.Equal(esPremium ? LimiteDiario.Premium : LimiteDiario.Estandar, limite);
    }

    [Fact]
    public void Limite_con_maximo_cero_o_negativo_lanza_excepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LimiteDiario(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LimiteDiario(-5));
    }
}
