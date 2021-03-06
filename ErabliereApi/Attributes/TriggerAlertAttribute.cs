﻿using ErabliereApi.Depot.Sql;
using ErabliereApi.Donnees;
using ErabliereApi.Donnees.Action.Post;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using static System.Text.Json.JsonSerializer;
using static System.Environment;
using static System.IO.File;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ErabliereApi.Controllers.Attributes
{
    /// <summary>
    /// Classe qui permet de rechercher et lancer les alertes relier à une action.
    /// </summary>
    public class TriggerAlertAttribute : ActionFilterAttribute
    {
        private int? _idErabliere;
        private PostDonnee? _donnee;

        /// <summary>
        /// Contructeur par initialisation.
        /// </summary>
        /// <param name="order">Ordre d'exectuion des action filter</param>
        public TriggerAlertAttribute(int order = int.MinValue)
        {
            Order = order;
        }

        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var id = context.ActionArguments["id"].ToString();

            _idErabliere = int.Parse(id ?? throw new InvalidOperationException("Le paramètre Id est requis dans la route pour utiliser l'attribue 'TriggerAlert'."));

            _donnee = context.ActionArguments.Single(a => a.Value?.GetType() == typeof(PostDonnee)).Value as PostDonnee;
        }

        /// <inheritdoc />
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Canceled == false)
            {
                try
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<TriggerAlertAttribute>>();

                    var depot = context.HttpContext.RequestServices.GetRequiredService<ErabliereDbContext>();

                    var alertes = depot.Alertes.AsNoTracking().Where(a => a.IdErabliere == _idErabliere).ToArray();

                    for (int i = 0; i < alertes.Length; i++)
                    {
                        var alerte = alertes[i];

                        MaybeTriggerAlerte(alerte, logger);
                    }
                }
                catch (Exception e)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<TriggerAlertAttribute>>();

                    logger.LogCritical(new EventId(92837485, "TriggerAlertAttribute.OnActionExecuted"), e, "Une erreur imprévue est survenu lors de l'execution de la fonction d'alertage.");
                }
            }
        }

        private void MaybeTriggerAlerte(Alerte alerte, ILogger<TriggerAlertAttribute> logger)
        {
            if (_donnee == null)
            {
                throw new InvalidOperationException("La donnée membre '_donnee' doit être initialiser pour utiliser la fonction d'alertage.");
            }

            var validationCount = 0;
            var conditionMet = 0;

            if (alerte.NiveauBassinThresholdHight != null && short.TryParse(alerte.NiveauBassinThresholdHight, out short nbth))
            {
                validationCount++;

                if (nbth > _donnee.NB)
                {
                    conditionMet++;
                }
            }

            if (alerte.NiveauBassinThresholdLow != null && short.TryParse(alerte.NiveauBassinThresholdLow, out short nbtl))
            {
                validationCount++;

                if (nbtl < _donnee.NB)
                {
                    conditionMet++;
                }
            }

            if (alerte.VacciumThresholdHight != null && short.TryParse(alerte.VacciumThresholdHight, out short vth))
            {
                validationCount++;

                if (vth > _donnee.V)
                {
                    conditionMet++;
                }
            }

            if (alerte.VacciumThresholdLow != null && short.TryParse(alerte.VacciumThresholdLow, out short vtl))
            {
                validationCount++;

                if (vtl < _donnee.V)
                {
                    conditionMet++;
                }
            }

            if (alerte.TemperatureThresholdHight != null && short.TryParse(alerte.TemperatureThresholdHight, out short tth))
            {
                validationCount++;

                if (tth > _donnee.T)
                {
                    conditionMet++;
                }
            }

            if (alerte.TemperatureThresholdLow != null && short.TryParse(alerte.TemperatureThresholdLow, out short ttl))
            {
                validationCount++;

                if (ttl < _donnee.T)
                {
                    conditionMet++;
                }
            }

            if (validationCount > 0 && validationCount == conditionMet)
            {
                TriggerAlerte(alerte, logger);
            }
        }

        private static readonly EmailConfig? _emailConfig = TryDeserializeEmailConfig();

        /// <summary>
        /// Fonction utilisé pour désérialiser les configurations permettant l'envoie de courriel
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private static EmailConfig? TryDeserializeEmailConfig()
        {
            try
            {
                var v = ReadAllText(GetEnvironmentVariable("EMAIL_CONFIG_PATH"));

                return Deserialize<EmailConfig>(v);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Erreur en désérialisant les configurations de l'email. La fonctionnalité des alertes ne pourra pas être utilisé.");
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
            }

            return default;
        }

        private async Task TriggerAlerte(Alerte alerte, ILogger<TriggerAlertAttribute> logger)
        {
            if (_emailConfig == null)
            {
                logger.LogWarning("Les configurations du courriel sont null, la fonctionnalité d'alerte ne peut pas fonctionner.");

                return;
            }

            try
            {
                if (alerte.EnvoyerA != null)
                {
                    var mailMessage = new MimeMessage();
                    mailMessage.From.Add(new MailboxAddress("ErabliereAPI - Alerte Service", _emailConfig.Sender));
                    foreach (var destinataire in alerte.EnvoyerA.Split(';'))
                    {
                        mailMessage.To.Add(MailboxAddress.Parse(destinataire));
                    }
                    mailMessage.Subject = $"Alerte ID : {alerte.Id}";
                    mailMessage.Body = new TextPart("plain")
                    {
                        Text = FormatTextMessage(alerte, _donnee)
                    };

                    using var smtpClient = new SmtpClient();
                    await smtpClient.ConnectAsync(_emailConfig.SmtpServer, _emailConfig.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await smtpClient.AuthenticateAsync(_emailConfig.Email, _emailConfig.Password);
                    await smtpClient.SendAsync(mailMessage);
                    await smtpClient.DisconnectAsync(true);
                }
            }
            catch (Exception e)
            {
                logger.LogCritical(new EventId(92837486, "TriggerAlertAttribute.TriggerAlerte"), e, "Une erreur imprévue est survenu lors de l'envoie de l'alerte.");
            }
        }

        private static string FormatTextMessage(Alerte alerte, PostDonnee? donnee)
        {
            var sb = new StringBuilder();

            sb.Append(nameof(Alerte));
            sb.AppendLine(" : ");
            sb.AppendLine(Serialize(alerte, _mailSerializerSettings));
            sb.AppendLine();
            sb.Append(nameof(PostDonnee));
            sb.AppendLine(" : ");
            sb.AppendLine(Serialize(donnee, _mailSerializerSettings));
            sb.AppendLine();
            sb.AppendLine($"Date : {DateTimeOffset.UtcNow}");

            return sb.ToString();
        }

        private readonly static JsonSerializerOptions _mailSerializerSettings = new() { WriteIndented = true };
    }
}