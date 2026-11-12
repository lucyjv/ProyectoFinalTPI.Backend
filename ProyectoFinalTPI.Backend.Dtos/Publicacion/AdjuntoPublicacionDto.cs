using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion;

public sealed record AdjuntoPublicacionDto(string Url, MultimediaEnum Tipo, int Orden);
public sealed record AdjuntoEntradaDto(Stream? Archivo, string? NombreArchivo, string? Url, MultimediaEnum Tipo);
public sealed record MusicaPublicacionDto(string SpotifyId, string Titulo, string Artista);