using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.MaterialProperty;

public class SimController : MonoBehaviour
{
    [Tooltip("Размер поля")]
    public Vector2Int fieldSize = new Vector2Int(1024, 512);
    public float stiffness = 1.0f;
    public float simSpeed = 1.0f;
    public ComputeShader computeShader;
    public Shader shader;

    private int threadSize = 8;

    private RenderTexture textureA;
    private RenderTexture textureB;
    private bool isBufferAInput = true;
    private Material material;

    private int kernelID;
    private int inputTexID;
    private int outputTexID;
    private int fieldSizeID;
    private int stiffnessID;
    private int deltaTimeID;
    private int baseTextureID;

    private Vector4 sizeVector;

    private void Start()
    {
        material = new Material(shader);
        kernelID = computeShader.FindKernel("CSMain");
        inputTexID = Shader.PropertyToID("_Input");
        outputTexID = Shader.PropertyToID("_Output");
        fieldSizeID = Shader.PropertyToID("_FieldSize");
        stiffnessID = Shader.PropertyToID("_Stiffness");
        deltaTimeID = Shader.PropertyToID("_DeltaTime");
        baseTextureID = Shader.PropertyToID("_BaseTexture");

        textureA = CreateRT();
        textureB = CreateRT();
        sizeVector = new Vector4(fieldSize.x, fieldSize.y, 0, 0);
    }

    private void FixedUpdate()
    {
        RenderTexture input = isBufferAInput ? textureA : textureB;
        RenderTexture output = isBufferAInput ? textureB : textureA;
        isBufferAInput = !isBufferAInput;

        computeShader.SetTexture(kernelID, inputTexID, input);
        computeShader.SetTexture(kernelID, outputTexID, output);
        computeShader.SetVector(fieldSizeID, sizeVector);
        computeShader.SetFloat(stiffnessID, stiffness);
        computeShader.SetFloat(deltaTimeID, Time.fixedDeltaTime * simSpeed);
        computeShader.Dispatch(kernelID, fieldSize.x / threadSize, fieldSize.y / threadSize, 1);

        material.SetTexture(baseTextureID, output);
    }

    private void OnDestroy()
    {
        if (textureA != null)
        {
            textureA.Release();
            Destroy(textureA);
        }

        if (textureB != null)
        {
            textureB.Release();
            Destroy(textureB);
        }
    }

    RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(fieldSize.x, fieldSize.y, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
}
