using UnityEngine;

public class CameraRenderer : MonoBehaviour
{
    private Camera mainCam;
    public Camera captureCam;
    private float offsetX;
    private float offsetY;
    private int pixelSize;

    void Start()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        //pixelSize proporcional al tamanio de la pantalla ( se mostraran pixeles en una escala de 27 por unidad de posicion)
        pixelSize = 8;
        Debug.Log(pixelSize);

        //defino el offset en un valor entre 0 y 1
        offsetX = mainCam.transform.position.x % 1;

        //redondeo el offset entre valores de pixeles en pantalla (cada unidad en pantalla tiene 27 pixeles)
        if (offsetX >= 0) offsetX = Mathf.Floor(offsetX * pixelSize) / pixelSize;
        else offsetX = Mathf.Ceil(offsetX * pixelSize) / pixelSize;
        //Debug.Log(offsetX);

        //realizo un redondeo de la posicion de la camaraMain para sumarle al offset y luego aplicar la posicion
        if (mainCam.transform.position.x >= 0) offsetX += Mathf.Floor(mainCam.transform.position.x);
        else offsetX += Mathf.Ceil(mainCam.transform.position.x);

        //-------- Realizo lo mismo pero para la Y axis

        offsetY = mainCam.transform.position.y % 1;

        if (offsetY >= 0) offsetY = Mathf.Floor(offsetY * pixelSize) / pixelSize;
        else offsetY = Mathf.Ceil(offsetY * pixelSize) / pixelSize;


        if (mainCam.transform.position.y >= 0) offsetY += Mathf.Floor(mainCam.transform.position.y);
        else offsetY += Mathf.Ceil(mainCam.transform.position.y);


        //aplico la posicion
        captureCam.transform.position = new Vector3(offsetX, offsetY, captureCam.transform.position.z);


    }
}
