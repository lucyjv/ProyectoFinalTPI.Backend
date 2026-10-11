namespace ProyectoFinalTPI.Backend.Entidades.ValueObjects
{
    /// <summary>
    /// Representa el nivel de categorización de un usuario dentro de la plataforma.
    /// Encapsula las reglas de progresión: qué nivel corresponde a cuántas publicaciones,
    /// y qué funciones desbloquea cada nivel.
    /// Relacionado con la tarea "Sistema de categorías de usuario" del Sprint Backlog.
    /// </summary>
    public sealed class NivelCategoria : IEquatable<NivelCategoria>
    {
        public int Numero { get; }
        public string Nombre { get; }
        public int PublicacionesRequeridas { get; }

        private static readonly IReadOnlyList<NivelCategoria> _niveles;

        // Definición centralizada de los niveles: aquí se cambian los umbrales
        static NivelCategoria()
        {
            _niveles = new List<NivelCategoria>
            {
                new NivelCategoria(1, "Nostálgico Novato",       publicacionesRequeridas: 0),
                new NivelCategoria(2, "Viajero del Tiempo",      publicacionesRequeridas: 5),
                new NivelCategoria(3, "Guardián de Recuerdos",   publicacionesRequeridas: 20),
                new NivelCategoria(4, "Cronista de Épocas",      publicacionesRequeridas: 50),
                new NivelCategoria(5, "Leyenda Nostálgica",      publicacionesRequeridas: 100),
            };
        }

        private NivelCategoria(int numero, string nombre, int publicacionesRequeridas)
        {
            Numero = numero;
            Nombre = nombre;
            PublicacionesRequeridas = publicacionesRequeridas;
        }

        /// <summary>Devuelve todos los niveles disponibles, de menor a mayor.</summary>
        public static IReadOnlyList<NivelCategoria> Todos() => _niveles;

        /// <summary>
        /// Calcula el nivel que corresponde según la cantidad de publicaciones del usuario.
        /// Siempre devuelve el nivel más alto alcanzado.
        /// </summary>
        public static NivelCategoria CalcularPara(int cantidadPublicaciones)
        {
            if (cantidadPublicaciones < 0)
                throw new ArgumentOutOfRangeException(nameof(cantidadPublicaciones),
                    "La cantidad de publicaciones no puede ser negativa.");

            return _niveles
                .Where(n => cantidadPublicaciones >= n.PublicacionesRequeridas)
                .OrderByDescending(n => n.Numero)
                .First();
        }

        /// <summary>Devuelve el siguiente nivel, o null si ya es el máximo.</summary>
        public NivelCategoria? SiguienteNivel() =>
            _niveles.FirstOrDefault(n => n.Numero == Numero + 1);

        /// <summary>Publicaciones que faltan para el siguiente nivel, o 0 si es el máximo.</summary>
        public int PublicacionesParaSubir(int publicacionesActuales)
        {
            var siguiente = SiguienteNivel();
            if (siguiente is null) return 0;
            return Math.Max(0, siguiente.PublicacionesRequeridas - publicacionesActuales);
        }

        public bool Equals(NivelCategoria? otro) => otro is not null && Numero == otro.Numero;
        public override bool Equals(object? obj) => obj is NivelCategoria n && Equals(n);
        public override int GetHashCode() => Numero.GetHashCode();
        public override string ToString() => $"Nivel {Numero} — {Nombre}";

        public static bool operator ==(NivelCategoria? a, NivelCategoria? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(NivelCategoria? a, NivelCategoria? b) => !(a == b);
    }
}
