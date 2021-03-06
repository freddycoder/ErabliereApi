﻿using ErabliereApi.Depot.Sql;
using ErabliereApi.Donnees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ErabliereApi.Controllers
{
    /// <summary>
    /// Contrôler pour interagir avec les alertes
    /// </summary>
    [ApiController]
    [Route("erablieres/{id}/[controller]")]
    [Authorize]
    public class AlertesController : ControllerBase
    {
        private readonly ErabliereDbContext _depot;

        /// <summary>
        /// Constructeur par initialisation
        /// </summary>
        /// <param name="depot"></param>
        public AlertesController(ErabliereDbContext depot)
        {
            _depot = depot;
        }

        /// <summary>
        /// Liste les Alertes
        /// </summary>
        /// <param name="id">Identifiant de l'érablière</param>
        /// <remarks>Les valeurs numérique sont en dixième de leurs unitées respective.</remarks>
        /// <response code="200">Une liste d'alerte potentiellement vide.</response>
        [HttpGet]
        public async Task<IEnumerable<Alerte>> Lister(int id)
        {
            return await _depot.Alertes.AsNoTracking().Where(b => b.IdErabliere == id).ToArrayAsync();
        }

        /// <summary>
        /// Ajouter une Alerte
        /// </summary>
        /// <remarks>Chaque valeur numérique est en dixième. Donc pour représenter 1 degré celcius, il faut inscrire 10.</remarks>
        /// <param name="id">L'identifiant de l'érablière</param>
        /// <param name="alerte">Les paramètres de l'alerte</param>
        /// <response code="200">L'alerte a été correctement ajouter.</response>
        /// <response code="400">L'id de la route ne concorde pas avec l'id de l'alerte à ajouter.</response>
        [HttpPost]
        public async Task<IActionResult> Ajouter(int id, Alerte alerte)
        {
            if (id != alerte.IdErabliere)
            {
                return BadRequest("L'id de la route ne concorde pas avec l'id du baril à ajouter");
            }

            await _depot.Alertes.AddAsync(alerte);

            await _depot.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// Modifier une alerte
        /// </summary>
        /// <param name="id">L'identifiant de l'érablière</param>
        /// <param name="alerte">L'alerte a modifier</param>
        /// <response code="200">L'alerte a été correctement supprimé.</response>
        /// <response code="400">L'id de la route ne concorde pas avec l'id du baril à modifier.</response>
        [HttpPut]
        public async Task<IActionResult> Modifier(int id, Alerte alerte)
        {
            if (id != alerte.IdErabliere)
            {
                return BadRequest("L'id de la route ne concorde pas avec l'id du baril à modifier.");
            }

            _depot.Update(alerte);

            await _depot.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// Supprimer une alerte
        /// </summary>
        /// <param name="id">Identifiant de l'érablière</param>
        /// <param name="alerte">L'alerte a supprimer</param>
        /// <response code="202">L'alerte a été correctement supprimé.</response>
        /// <response code="400">L'id de la route ne concorde pas avec l'id de l'alerte à supprimer.</response>
        [HttpDelete]
        public async Task<IActionResult> Supprimer(int id, Alerte alerte)
        {
            if (id != alerte.IdErabliere)
            {
                return BadRequest("L'id de la route ne concorde pas avec l'id du baril à supprimer.");
            }

            _depot.Remove(alerte);

            await _depot.SaveChangesAsync();

            return NoContent();
        }
    }
}
