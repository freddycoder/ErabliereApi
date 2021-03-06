import { DonneeCapteur } from "./donneeCapteur";

export class Capteur {
    id?: number;
    nom?: string;
    symbole?: string;
    afficherCapteurDashboard?: boolean;
    dc?: string;
    donnees?: DonneeCapteur[];
}