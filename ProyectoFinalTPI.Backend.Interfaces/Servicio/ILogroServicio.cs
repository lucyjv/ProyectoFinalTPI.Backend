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
        void VerificarYDarLogro(int idUsuario, int idLogro);
        void MostrarLogrosDeUsuario(int idUsuario);

    }
}
