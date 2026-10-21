using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class CatalogoAnalisisRepositorio(ApplicationDbContext db) : ICatalogoAnalisisRepositorio
{
    public async Task<IReadOnlyList<CandidatoMomentoDto>> BuscarCandidatos(string texto, CancellationToken ct)
    {
        var palabras = Regex.Matches(NormalizadorMomento.Normalizar(texto), @"[\p{L}\p{N}]+")
            .Select(m => m.Value).Where(p => p.Length >= 4 || p.All(char.IsDigit))
            .Distinct().OrderByDescending(p => p.All(char.IsDigit)).ThenByDescending(p => p.Length).Take(12).ToArray();
        if (palabras.Length == 0) return [];
        Expression<Func<Momento, bool>> filtro = m => false;
        foreach (var palabra in palabras)
        {
            Expression<Func<Momento, bool>> condicion = m => m.Nombre.ToLower().Contains(palabra) || m.Alias.Any(a => a.TextoNormalizado.Contains(palabra));
            var cuerpo = new ReemplazarParametro(condicion.Parameters[0], filtro.Parameters[0]).Visit(condicion.Body)!;
            filtro = Expression.Lambda<Func<Momento, bool>>(Expression.OrElse(filtro.Body, cuerpo), filtro.Parameters);
        }
        // Búsqueda acotada: no enviar todo el catálogo ni otras publicaciones al proveedor.
        return await db.Momentos.AsNoTracking()
            .Where(m => m.MomentoCanonicoId == null)
            .Where(filtro)
            .OrderBy(m => m.Id).Take(40)
            .Select(m => new CandidatoMomentoDto(m.Id, m.Nombre, m.FechaInicio, m.FechaFin,
                m.Alias.OrderBy(a => a.Id).Take(8).Select(a => a.Texto).ToArray())).ToListAsync(ct);
    }

    private sealed class ReemplazarParametro(ParameterExpression origen, ParameterExpression destino) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == origen ? destino : base.VisitParameter(node);
    }
}
