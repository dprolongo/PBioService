//------------------------------------------------------------------------------
// <auto-generated>
//    Este código se generó a partir de una plantilla.
//
//    Los cambios manuales en este archivo pueden causar un comportamiento inesperado de la aplicación.
//    Los cambios manuales en este archivo se sobrescribirán si se regenera el código.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PBioSvc
{
    using System;
    using System.Collections.Generic;
    
    public partial class Archivo
    {
        public System.Guid IdArchivo { get; set; }
        public System.Guid IdCarpeta { get; set; }
        public string Usuario { get; set; }
        public bool Publico { get; set; }
        public string NombreArchivo { get; set; }
        public string ContentType { get; set; }
        public System.DateTime FechaCreacion { get; set; }
        public string Descripcion { get; set; }
        public bool BaseDatos { get; set; }
        public string Datos { get; set; }
    
        public virtual Carpeta Carpeta { get; set; }
    }
}
