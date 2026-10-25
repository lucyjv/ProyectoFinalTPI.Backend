using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio
{
    public interface ILogroServicio
    {
        Task VerificarYDarLogro(int idUsuario, LogroEnum logro);
        Task<List<Logros_Usuarios>> MostrarLogrosDeUsuario(int idUsuario);
        Task<List<ProyectoFinalTPI.Backend.Dtos.Usuario.LogroEstadoDto>> ObtenerCatalogoParaUsuario(int idUsuario);

    }
}
