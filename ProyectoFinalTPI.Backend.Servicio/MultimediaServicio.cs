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

        public MultimediaServicio(IConfiguration configuration)
        {
            _cloudinary = new Lazy<Cloudinary>(() =>
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
            });
        }
        public async Task<string> SubirAsync(Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo)
        {
            // YouTube entrega una página HTML, no un archivo que Cloudinary pueda subir.
            // Conservamos el enlace para reproducirlo con el reproductor de YouTube.
            if (archivo == null && tipo == MultimediaEnum.Video &&
                Uri.TryCreate(urlExterna, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps && EsHostYouTube(uri.Host))
            {
                return urlExterna!;
            }

            var cloudinary = _cloudinary.Value;
            var file = archivo != null
            ? new FileDescription(nombreArchivo, archivo)
            : new FileDescription(urlExterna);

            if (tipo == MultimediaEnum.Video)
            {
                var resultado = await cloudinary.UploadAsync(
                    new VideoUploadParams
                    {
                        File = file,
                        Folder = "nostalgiar/publicaciones"
                    });

                if (resultado.Error != null)
                    throw new InvalidOperationException(resultado.Error.Message);

                return resultado.SecureUrl.ToString();
            }

            var resultadoImagen = await cloudinary.UploadAsync(
                new ImageUploadParams
                {
                    File = file,
                    Folder = "nostalgiar/publicaciones"
                });

            if (resultadoImagen.Error != null)
                throw new InvalidOperationException(resultadoImagen.Error.Message);

            return resultadoImagen.SecureUrl.ToString();
        }

        private static bool EsHostYouTube(string host) =>
            host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }
}
