namespace ProyectoFinalTPI.Backend.Entidades.ValueObjects
{
    /// <summary>
    /// Representa una década (ej: 1980, 1990, 2000).
    /// El valor siempre es el año de inicio de la década (múltiplo de 10).
    /// </summary>
    public sealed class Decada : IEquatable<Decada>
    {
        public int Valor { get; }

        // Décadas que la plataforma Nostalgia soporta explícitamente
        private static readonly int[] DecadasSoportadas = { 1910, 1920, 1930, 1940, 1950, 1960, 1970, 1980, 1990, 2000, 2010, 2020 };

        public Decada(int valor)
        {
            if (valor % 10 != 0)
                throw new ArgumentException(
                    $"Una década debe ser múltiplo de 10. Se recibió: {valor}.",
                    nameof(valor));

            if (valor < 1900 || valor > DateTime.UtcNow.Year)
                throw new ArgumentOutOfRangeException(
                    nameof(valor),
                    $"La década debe estar entre 1900 y la actual. Se recibió: {valor}.");

            Valor = valor;
        }

        /// <summary>Infiere la década a partir de cualquier año.</summary>
        public static Decada DeAño(int año) => new Decada(año - (año % 10));

        /// <summary>Devuelve la etiqueta de display (ej: "80s", "90s", "2000s").</summary>
        public string Etiqueta()
        {
            int siglo = Valor / 100 * 100;
            int decenaEnSiglo = Valor % 100;
            return decenaEnSiglo == 0 ? $"{Valor}s" : $"{decenaEnSiglo}s";
        }

        public bool Equals(Decada? otro) => otro is not null && Valor == otro.Valor;
        public override bool Equals(object? obj) => obj is Decada d && Equals(d);
        public override int GetHashCode() => Valor.GetHashCode();
        public override string ToString() => Etiqueta();

        public static bool operator ==(Decada? a, Decada? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(Decada? a, Decada? b) => !(a == b);
    }
}
