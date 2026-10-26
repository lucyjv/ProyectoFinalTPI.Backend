using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class MultimediaServicio : IMultimediaServicio
    {
        private readonly Cloudinary _cloudinary;

        public MultimediaServicio(IConfiguration configuration)
        {
            var account = new Account(
                configuration["Cloudinary:CloudName"],
                configuration["Cloudinary:ApiKey"],
                configuration["Cloudinary:ApiSecret"]);

            _cloudinary = new Cloudinary(account);
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

            var file = archivo != null
            ? new FileDescription(nombreArchivo, archivo)
            : new FileDescription(urlExterna);

            if (tipo == MultimediaEnum.Video)
            {
                var resultado = await _cloudinary.UploadAsync(
                    new VideoUploadParams
                    {
                        File = file,
                        Folder = "nostalgiar/publicaciones"
                    });

                if (resultado.Error != null)
                    throw new InvalidOperationException(resultado.Error.Message);

                return resultado.SecureUrl.ToString();
            }

            var resultadoImagen = await _cloudinary.UploadAsync(
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
