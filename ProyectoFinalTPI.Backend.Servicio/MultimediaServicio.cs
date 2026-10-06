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
    }
}
