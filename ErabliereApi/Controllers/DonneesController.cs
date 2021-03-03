﻿using AutoMapper;
using ErabliereApi.Attributes;
using ErabliereApi.Depot;
using ErabliereApi.Donnees;
using ErabliereApi.Donnees.Action.Get;
using ErabliereApi.Donnees.Action.Post;
using ErabliereApi.Donnees.Action.Put;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ErabliereApi.Controllers
{
    /// <summary>
    /// Contrôler représentant les données reçu par l'automate principale
    /// </summary>
    [ApiController]
    [Route("erablieres/{id}/[controller]")]
    public class DonneesController : ControllerBase
    {
        private readonly Depot<Donnee> _depot;
        private readonly IMapper _mapper;

        /// <summary>
        /// Constructeur par initlisation
        /// </summary>
        /// <param name="depot"></param>
        /// <param name="mapper">Mapper entre les modèles</param>
        public DonneesController(Depot<Donnee> depot, IMapper mapper)
        {
            _depot = depot;
            _mapper = mapper;
        }

        /// <summary>
        /// Lister les données
        /// </summary>
        /// <param name="id">L'identifiant de l'érablière</param>
        /// <param name="dd">Date de début</param>
        /// <param name="df">Date de début</param>
        /// <param name="q">Quantité de donnée demander</param>
        /// <param name="o">Doit être croissant "c" ou decroissant "d". Par défaut "c"</param>
        /// <param name="ddr">Date de la dernière données reçu. Permet au client d'optimiser le nombres de données reçu.</param>
        /// <response code="200">Retourne une liste de données. La liste est potentiellement vide.</response>
        [ProducesResponseType(200, Type = typeof(GetDonnee))]
        [HttpGet]
        public IActionResult Lister(int id,
                                    [FromHeader(Name = "x-ddr")] DateTimeOffset? ddr,
                                    DateTimeOffset? dd, 
                                    DateTimeOffset? df, 
                                    int? q, 
                                    string? o = "c")
        {
            var query = _depot.Lister(d => d.IdErabliere == id &&
                                      (ddr == null || d.D > ddr) &&
                                      (dd == null || d.D >= dd) &&
                                      (df == null || d.D <= df));

            if (o == "d")
            {
                query = query.OrderByDescending(d => d);
            }

            if (q.HasValue)
            {
                query = query.Take(q.Value);
            }

            var list = query.ToList();

            if (o == "c" && list.Count > 0)
            {
                if (ddr.HasValue)
                {
                    HttpContext.Response.Headers.Add("x-ddr", ddr.Value.ToString());
                }
                HttpContext.Response.Headers.Add("x-dde", query.Last().D.ToString());
            }

            return Ok(list.Select(d => new { d.Id, d.D, d.T, d.V, d.NB, d.Iddp, d.Nboc, d.PI }));
        }

        /// <summary>
        /// Ajouter un donnée.
        /// </summary>
        /// <param name="id">L'identifiant de l'érablière</param>
        /// <param name="donneeRecu">La donnée à ajouter</param>
        [HttpPost]
        [ValiderIPRules]
        public async Task<IActionResult> Ajouter(int id, PostDonnee donneeRecu)
        {
            if (id != donneeRecu.IdErabliere)
            {
                return BadRequest($"L'id de la route '{id}' ne concorde pas avec l'id de l'érablière dans la donnée '{donneeRecu.IdErabliere}'.");
            }

            if (donneeRecu.D == default || donneeRecu.D.Equals(DateTimeOffset.MinValue))
            {
                donneeRecu.D = DateTimeOffset.Now;
            }

            var donnePlusRecente = _depot.Lister(d => d.IdErabliere == id).LastOrDefault();

            if (donnePlusRecente != null &&
                donnePlusRecente.IdentiqueMemeLigneDeTemps(donneeRecu))
            {
                if (donnePlusRecente.D.HasValue == false || donneeRecu.D.HasValue == false)
                {
                    throw new InvalidProgramException("Les dates des données ne devrait pas être null rendu à cette étape.");
                }

                var interval = donneeRecu.D.Value - donnePlusRecente.D.Value;

                if (donnePlusRecente.Iddp != null)
                {
                    donnePlusRecente.D = donneeRecu.D;

                    if (donnePlusRecente.PI.HasValue == false)
                    {
                        if (interval.Milliseconds > donnePlusRecente.PI)
                        {
                            donnePlusRecente.PI = (int)interval.TotalSeconds;
                        }
                    }
                    else
                    {
                        donnePlusRecente.PI = (int)interval.TotalSeconds;
                    }

                    donnePlusRecente.Nboc++;

                    await _depot.ModifierAsync(donnePlusRecente);
                }
                else
                {
                    var donnee = _mapper.Map<Donnee>(donneeRecu);

                    donnee.Iddp = donnePlusRecente.Id;

                    donnee.PI = (int)interval.TotalSeconds;

                    await _depot.AjouterAsync(donnee);
                }
            }
            else
            {
                await _depot.AjouterAsync(_mapper.Map<Donnee>(donneeRecu));
            }

            return Ok();
        }

        /// <summary>
        /// Modifier un dompeux
        /// </summary>
        /// <param name="id">Identifiant de l'érablière</param>
        /// <param name="idDonnee">L'id de la donnée à modifier</param>
        /// <param name="donnee">Le dompeux à ajouter</param>
        [HttpPut("{idDonnee}")]
        [ValiderIPRules]
        public IActionResult Modifier(int id, int idDonnee, PutDonnee donnee)
        {
            if (id != donnee.IdErabliere)
            {
                return BadRequest("L'id de la route ne concorde pas avec l'id du dompeux.");
            }

            if (idDonnee != donnee.Id)
            {
                return BadRequest("L'id de la donnée dans la route ne concorde pas avec l'id la donnée dans le body.");
            }

            var entity = _depot.Obtenir(donnee.Id);

            if (entity == null)
            {
                return BadRequest($"La donnée que vous tentez de modifier n'existe pas.");
            }

            if (entity.IdErabliere != donnee.IdErabliere)
            {
                return BadRequest($"L'id de l'érablière de la donnée trouvé ne concorde pas avec l'id de l'érablière de la route.");
            }

            // fin des validations

            if (donnee.V.HasValue)
            {
                entity.V = donnee.V;
            }

            _depot.Modifier(entity);
            

            return Ok();
        }

        /// <summary>
        /// Supprimer un dompeux
        /// </summary>
        /// <param name="id">L'identifiant de l'érablière</param>
        /// <param name="donnee">Le dompeux a supprimer</param>
        [HttpDelete("{idDonnee}")]
        [ValiderIPRules]
        public IActionResult Supprimer(int id, int idDonnee, Donnee donnee)
        {
            if (id != donnee.IdErabliere)
            {
                return BadRequest("L'id de la route ne concorde pas avec l'id de la donnée.");
            }

            if (idDonnee != donnee.Id)
            {
                return BadRequest("L'id de la donnée dans la route ne concorde pas avec l'id la donnée dans le body.");
            }

            var entity = _depot.Obtenir(donnee.Id);

            if (entity == null)
            {
                return BadRequest($"La donnée que vous tentez de supprimer n'existe pas.");
            }

            if (entity.IdErabliere != donnee.IdErabliere)
            {
                return BadRequest($"L'id de l'érablière de la donnée trouvé ne concorde pas avec l'id de l'érablière de la route.");
            }

            _depot.Supprimer(entity);

            return NoContent();
        }
    }
}
