using UnityEngine;

/// <summary>
/// CivCiv 3D - Etkilesimli Nesne Arayuzu (IInteractable)
/// Dunyadaki etkilesilebilir nesneleri (basinc plakasi, kapi, sandik, dugme vb.) standartlastirir.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Belirtilen PlayerInteractor bu nesneyle su anda etkilesime gecebilir mi?
    /// </summary>
    bool CanInteract(PlayerInteractor interactor);

    /// <summary>
    /// Oyuncu nesneye yaklastiginda ekranda goruntulenecek dugme/eylem ipucu metni.
    /// </summary>
    string GetPromptText(PlayerInteractor interactor);

    /// <summary>
    /// Etkilesimi gerceklestirir. Etkilesim basariyla tamamlandiysa true doner.
    /// </summary>
    bool Interact(PlayerInteractor interactor);

    /// <summary>
    /// Etkilesimli nesnenin dunya konumu ve Transform bileseni.
    /// </summary>
    Transform transform { get; }
}
