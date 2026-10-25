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
    }
}
