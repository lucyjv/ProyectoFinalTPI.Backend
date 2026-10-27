using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class MultimediaServicio : IMultimediaServicio
    {
        private readonly Lazy<Cloudinary> _cloudinary;
        private const string CarpetaPublicaciones = "nostalgiar/publicaciones";

        public MultimediaServicio(IConfiguration configuration)
        {
            _cloudinary = new Lazy<Cloudinary>(() => CrearClienteCloudinary(configuration));
        }

        #region Operaciones públicas

        public async Task<string> SubirAsync(Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo)
        {
            if (EsVideoYouTube(archivo, urlExterna, tipo)) return urlExterna!;

            var cloudinary = _cloudinary.Value;
            var archivoSubida = CrearArchivo(archivo, nombreArchivo, urlExterna);
            var resultado = tipo == MultimediaEnum.Video
                ? await SubirVideo(cloudinary, archivoSubida)
                : await SubirImagen(cloudinary, archivoSubida);
            return ObtenerUrlSubida(resultado);
        }

        #endregion

        #region Métodos privados y validaciones

        private static Cloudinary CrearClienteCloudinary(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];
            if (string.IsNullOrWhiteSpace(cloudName)
                || string.IsNullOrWhiteSpace(apiKey)
                || string.IsNullOrWhiteSpace(apiSecret))
                throw new InvalidOperationException(
                    "Para subir archivos configurá Cloudinary:CloudName, Cloudinary:ApiKey y Cloudinary:ApiSecret.");
            return new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        }
        private static FileDescription CrearArchivo(Stream? archivo, string? nombreArchivo, string? urlExterna) =>
            archivo != null ? new FileDescription(nombreArchivo, archivo) : new FileDescription(urlExterna);
        private static async Task<UploadResult> SubirVideo(Cloudinary cloudinary, FileDescription archivoSubida) =>
            await cloudinary.UploadAsync(new VideoUploadParams { File = archivoSubida, Folder = CarpetaPublicaciones });
        private static async Task<UploadResult> SubirImagen(Cloudinary cloudinary, FileDescription archivoSubida) =>
            await cloudinary.UploadAsync(new ImageUploadParams { File = archivoSubida, Folder = CarpetaPublicaciones });
        private static string ObtenerUrlSubida(UploadResult resultado)
        {
            if (resultado.Error != null) throw new InvalidOperationException(resultado.Error.Message);
            return resultado.SecureUrl.ToString();
        }
        private static bool EsVideoYouTube(Stream? archivo, string? urlExterna, MultimediaEnum tipo) =>
            archivo == null && tipo == MultimediaEnum.Video &&
            Uri.TryCreate(urlExterna, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && EsHostYouTube(uri.Host);
        private static bool EsHostYouTube(string host) =>
            host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);

        #endregion
    }
}
