using UnityEngine;

public class CastleFireController : MonoBehaviour
{
    public GameObject fire1;
    public GameObject fire2;
    public GameObject fire3;
    public GameObject fire4;
    public GameObject explosion;
    public TheOneAndOnlyStats unitStats;

    public void ActivateFire()
    {
        if (unitStats.currentHealth / unitStats.maxHealth < 0.8f)
        {
            fire1.SetActive(true);
        }

        if (unitStats.currentHealth / unitStats.maxHealth < 0.6f)
        {
            fire2.SetActive(true);
        }

        if (unitStats.currentHealth / unitStats.maxHealth < 0.4f)
        {
            fire3.SetActive(true);
        }

        if (unitStats.currentHealth / unitStats.maxHealth < 0.2f)
        {
            fire4.SetActive(true);
        }

        if (unitStats.currentHealth / unitStats.maxHealth < 0.0f)
        {
            fire1.SetActive(false);
            fire2.SetActive(false);
            fire3.SetActive(false);
            fire4.SetActive(false);
            explosion.SetActive(true);
        }
    }

    private void Update()
    {
        ActivateFire();
    }

    private void Start()
    {
        fire1.SetActive(false);
        fire2.SetActive(false);
        fire3.SetActive(false);
        fire4.SetActive(false);
        explosion.SetActive(false);
    }
}