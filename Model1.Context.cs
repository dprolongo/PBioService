﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class webappDBEntities : DbContext
    {
        public webappDBEntities()
            : base("name=webappDBEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public DbSet<Archivo> Archivo { get; set; }
        public DbSet<Carpeta> Carpeta { get; set; }
        public DbSet<EstadoSimulacion> EstadoSimulacion { get; set; }
        public DbSet<Log> Log { get; set; }
        public DbSet<MetodoClasificacion> MetodoClasificacion { get; set; }
        public DbSet<MetodoSeleccion> MetodoSeleccion { get; set; }
        public DbSet<Proyecto> Proyecto { get; set; }
        public DbSet<Resultado> Resultado { get; set; }
        public DbSet<Simulacion> Simulacion { get; set; }
        public DbSet<sysdiagrams> sysdiagrams { get; set; }
    }
}
