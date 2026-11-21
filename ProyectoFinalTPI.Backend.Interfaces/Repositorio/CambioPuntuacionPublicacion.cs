using ProyectoFinalTPI.Backend.Dtos.Publicacion;

namespace ProyectoFinalTPI.Backend.Interfaces.Repositorio;

public sealed record CambioPuntuacionPublicacion(PuntuacionPublicacionDto Estado, int PuntosOtorgadosPorUsuario);
