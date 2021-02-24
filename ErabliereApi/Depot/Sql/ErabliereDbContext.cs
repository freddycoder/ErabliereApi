﻿using ErabliereApi.Donnees;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ErabliereApi.Depot.Sql
{
    /// <summary>
    /// Classe DbContext pour interagir avec la base de donnée en utilisant EntityFramework
    /// </summary>
    public class ErabliereDbContext : DbContext
    {
        /// <summary>
        /// Constructeur par initialisation
        /// </summary>
        /// <param name="options"></param>
        public ErabliereDbContext([NotNull] DbContextOptions options) : base(options)
        {
            
        }

        /// <summary>
        /// Table des alertes
        /// </summary>
        public DbSet<Alerte> Alertes { get; private set; }

        /// <summary>
        /// Table des barils
        /// </summary>
        public DbSet<Baril> Barils { get; private set; }

        /// <summary>
        /// Table des dompeux
        /// </summary>
        public DbSet<Dompeux> Dompeux { get; private set; }

        /// <summary>
        /// Table des données
        /// </summary>
        public DbSet<Donnee> Donnees { get; private set; }

        /// <summary>
        /// Table des érablières
        /// </summary>
        public DbSet<Erabliere> Erabliere { get; private set; }
    }
}
