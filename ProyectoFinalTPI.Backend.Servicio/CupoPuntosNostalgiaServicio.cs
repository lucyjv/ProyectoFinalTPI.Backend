using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CupoPuntosNostalgiaServicio(
    ICategoriaServicio categorias, IConsumoDiarioPuntosNostalgiaRepositorio consumos, TimeProvider reloj)
    : ICupoPuntosNostalgiaServicio
{
    public async Task<int> ObtenerCupoParaPuntuar(int usuarioId, PermisoCuenta permiso, CancellationToken ct = default) =>
        CupoPara(await categorias.ObtenerCategoriaConPermiso(usuarioId, permiso, ct));

    public async Task<SaldoDiarioPuntosNostalgiaDto> ObtenerSaldo(int usuarioId, CancellationToken ct = default)
    {
        var cupo = CupoPara(await categorias.ObtenerCategoriaEfectiva(usuarioId, ct));
        var fecha = CalendarioNostalgia.FechaLocal(reloj.GetUtcNow());
        var gastados = await consumos.ObtenerPuntosGastados(usuarioId, fecha, ct);
        return new(cupo, Math.Max(0, cupo - gastados), CalendarioNostalgia.InicioDelDia(fecha.AddDays(1)));
    }

    private static int CupoPara(CategoriaCuenta categoria) => categoria switch
    {
        CategoriaCuenta.Nostalgico => 5,
        CategoriaCuenta.Aedo => 7,
        CategoriaCuenta.Mnemosine or CategoriaCuenta.Marca => 10,
        _ => 0
    };
}
