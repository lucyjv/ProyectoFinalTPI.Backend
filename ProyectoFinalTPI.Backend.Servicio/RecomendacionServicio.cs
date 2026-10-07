using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class RecomendacionServicio : IRecomendacionServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IInteresServicio _interesServicio;

        public RecomendacionServicio(
            IPublicacionRepositorio publicacionRepositorio,
            IInteresServicio interesServicio)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _interesServicio = interesServicio;
        }

        public async Task<IReadOnlyList<int>> GenerarRecomendacionParaUsuario(int usuarioId, int cantidadMaxima, CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero.");
            }

            if (cantidadMaxima < 1 || cantidadMaxima > 500)
            {
                throw new ArgumentException(
                    "La cantidad máxima debe estar entre 1 y 500.");
            }

            var intereses = await _interesServicio.ObtenerIntereses(
                usuarioId,
                cancellationToken);

            var afinidades = ConstruirAfinidades(intereses);

            var fechaCorte = DateTime.UtcNow;
            var limiteCandidatos = Math.Max(100, cantidadMaxima * 3);

            var candidatosGenerales =
                await _publicacionRepositorio.ObtenerPublicacionesParaVos(
                    Array.Empty<CategoriaEnum>(),
                    fechaCorte,
                    null,
                    limiteCandidatos,
                    cancellationToken);

            var candidatos = candidatosGenerales
                .Take(limiteCandidatos)
                .ToList();

            var categoriasPreferidas = afinidades
                .Where(a => a.Value > 0)
                .Select(a => a.Key)
                .ToArray();

            if (categoriasPreferidas.Length > 0)
            {
                var candidatosAfines =
                    await _publicacionRepositorio.ObtenerPublicacionesParaVos(
                        categoriasPreferidas,
                        fechaCorte,
                        null,
                        limiteCandidatos,
                        cancellationToken);

                candidatos.AddRange(
                    candidatosAfines.Take(limiteCandidatos));
            }

            var candidatosUnicos = candidatos
                .DistinctBy(p => p.Id)
                .ToList();

            if (candidatosUnicos.Count == 0)
            {
                return Array.Empty<int>();
            }

            var cantidadFinal = Math.Min(
                cantidadMaxima,
                candidatosUnicos.Count);

            var cantidadExploracion =
                (int)Math.Floor(cantidadFinal * 0.20);

            var cantidadPrincipal =
                cantidadFinal - cantidadExploracion;

            var principales = candidatosUnicos
                .Select(p => new
                {
                    Publicacion = p,
                    Puntaje = CalcularPuntaje(
                        p,
                        afinidades,
                        fechaCorte)
                })
                .OrderByDescending(p => p.Puntaje)
                .ThenByDescending(p => p.Publicacion.FechaCreacion)
                .ThenByDescending(p => p.Publicacion.Id)
                .Take(cantidadPrincipal)
                .Select(p => p.Publicacion.Id)
                .ToList();

            var idsPrincipales = principales.ToHashSet();

            var exploracion = candidatosUnicos
                .Where(p => !idsPrincipales.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    Orden = Random.Shared.NextDouble()
                })
                .OrderBy(p => p.Orden)
                .Take(cantidadExploracion)
                .Select(p => p.Id)
                .ToList();

            return Intercalar(principales, exploracion);
        }

        

        private static double CalcularPuntaje(
            PublicacionDetalleDto publicacion,
            IReadOnlyDictionary<CategoriaEnum, double> afinidades,
            DateTime fechaCorte)
        {
            afinidades.TryGetValue(
                publicacion.Categoria,
                out var afinidad);

            var diasDesdePublicacion = Math.Max(
                0,
                (fechaCorte - publicacion.FechaCreacion).TotalDays);

            var actualidad =
                1.0 / (1.0 + diasDesdePublicacion / 30.0);

            var variacion = Random.Shared.NextDouble();

            return afinidad * 0.60
                 + actualidad * 0.30
                 + variacion * 0.10;
        }

        private static List<int> Intercalar(
            IReadOnlyList<int> principales,
            IReadOnlyList<int> exploracion)
        {
            var resultado = new List<int>(
                principales.Count + exploracion.Count);

            var indicePrincipal = 0;
            var indiceExploracion = 0;

            while (indicePrincipal < principales.Count ||
                   indiceExploracion < exploracion.Count)
            {
                for (var i = 0;
                     i < 4 && indicePrincipal < principales.Count;
                     i++)
                {
                    resultado.Add(principales[indicePrincipal]);
                    indicePrincipal++;
                }

                if (indiceExploracion < exploracion.Count)
                {
                    resultado.Add(exploracion[indiceExploracion]);
                    indiceExploracion++;
                }
            }

            return resultado;
        }

        private static Dictionary<CategoriaEnum, double>ConstruirAfinidades(IReadOnlyList<InteresCategoriaDto> intereses)
        {
            var afinidades = new Dictionary<CategoriaEnum, double>();

            foreach (var interes in intereses)
            {
                if (!Enum.IsDefined(
                        typeof(CategoriaEnum),
                        interes.Categoria) ||
                    !double.IsFinite(interes.Afinidad) ||
                    interes.Afinidad < 0 ||
                    interes.Afinidad > 1)
                {
                    throw new InvalidOperationException(
                        "El servicio de intereses devolvió datos inválidos.");
                }

                if (!afinidades.TryAdd(
                    interes.Categoria,
                    interes.Afinidad))
                {
                    throw new InvalidOperationException(
                        "El servicio de intereses devolvió categorías repetidas.");
                }
            }

            return afinidades;
        }
    }
}
