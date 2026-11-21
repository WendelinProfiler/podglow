#pragma once
inline const std::map<std::wstring,std::wstring>& CompanionEnglishHelp(){
 static const std::map<std::wstring,std::wstring> text{
  {L"Wie möchtest du hören?",L"Choose a listening mode"},
  {L"Transparenz: Umgebung weiterhin hören.\nAdaptiv: automatisch an die Umgebung anpassen.\nGeräuschunterdrückung: Störgeräusche ausblenden.",L"Transparency: hear your surroundings.\nAdaptive: adjust to the environment automatically.\nNoise cancellation: reduce unwanted background sound."},
  {L"Wenn du sprichst, wird die Musik leiser.\nNach dem Gespräch kehrt sie zur\nvorherigen Lautstärke zurück.",L"Your music gets quieter when you speak.\nAfter the conversation, the volume\nreturns to its previous level."},
  {L"Automatische Lautstärke",L"Personalized volume"},
  {L"Die AirPods passen die Lautstärke an deine\nUmgebung und deine Hörgewohnheiten an.\nDu kannst den Regler weiterhin selbst bedienen.",L"AirPods adjust volume to your environment\nand listening habits. You can still change\nthe volume yourself."},
  {L"Musik pausiert, wenn du einen Hörer\nherausnimmst. Die AirPods verwenden dafür\nihre In-Ohr-Erkennung.",L"Music pauses when you remove an earbud.\nAirPods use their in-ear sensors\nto detect this."},
  {L"Schaltet das AirPods-Mikrofon in Windows stumm.\nMusik und andere Töne hörst du weiterhin.\nZum Sprechen den Schalter wieder ausschalten.",L"Mutes the AirPods microphone in Windows.\nYou can still hear music and other audio.\nTurn this off when you want to speak."},
  {L"Beim Verbinden wählt Windows das\nAirPods-Mikrofon. Beim Trennen wird das\nvorherige verfügbare Mikrofon wieder genutzt.",L"Connecting selects the AirPods microphone.\nDisconnecting restores your previous\navailable microphone."},
  {L"Welcher Hörer nimmt dich auf?",L"Which earbud should record your voice?"},
  {L"Automatisch wählt den passenden Hörer.\nLinks oder Rechts legt eine Seite fest.\nEmpfohlen: Automatisch.",L"Automatic chooses the appropriate earbud.\nLeft or Right fixes the microphone to one side.\nRecommended: Automatic."},
  {L"Legt fest, wie viel Zeit zwischen zwei\nDruckbewegungen am Stiel liegen darf.\nLangsamer erleichtert den Doppeldruck.",L"Sets how much time you have between\ntwo presses on the stem. A slower setting\nmakes double presses easier."},
  {L"Legt fest, wie lange du den Stiel gedrückt\nhalten musst, bis die hinterlegte Funktion\nausgelöst wird. Kürzer reagiert früher.",L"Sets how long you need to hold the stem\nbefore its assigned action starts.\nA shorter setting responds sooner."},
  {L"Betrifft die Signaltöne der AirPods, etwa\nbeim Verbinden und beim Hörmoduswechsel.\nDie Musiklautstärke bleibt unverändert.",L"Changes the AirPods' tones, such as\nconnection and listening-mode sounds.\nYour music volume stays the same."},
  {L"Ergänzt in der Hauptansicht eine vierte Taste.\nDamit lassen sich die aktiven Hörmodi\nausschalten, ohne die AirPods zu trennen.",L"Adds a fourth button to the main view.\nIt turns off active listening modes\nwithout disconnecting your AirPods."},
  {L"Linker AirPod",L"Left AirPod"},{L"Rechter AirPod",L"Right AirPod"},{L"Ladecase",L"Charging case"},{L"Noch kein Akkustand empfangen.",L"No battery reading received yet."},
  {L"Ohne aktive Geräuschkontrolle.",L"No active noise control."},{L"Blendet störende Umgebungsgeräusche aus.",L"Reduces unwanted background noise."},{L"Du hörst deine Umgebung weiterhin.",L"You can still hear your surroundings."},{L"Passt die Geräuschkontrolle an deine Umgebung an.",L"Adjusts noise control to your surroundings."},{L"Wähle, wie viel du von deiner Umgebung hörst.",L"Choose how much of your surroundings you hear."}
 };return text;
}
