using UnityEngine;

public class ComputeRunner : MonoBehaviour
{
    // Ссылка на наш Compute-шейдер, которую мы перетащим в инспекторе
    public ComputeShader computeShader;

    void Start()
    {
        int arraySize = 1_000_000;

        // 1. Создаем исходные массивы данных на CPU
        float[] dataA = new float[arraySize];
        float[] dataB = new float[arraySize];
        float[] dataResult = new float[arraySize];

        for (int i = 0; i < arraySize; i++)
        {
            dataA[i] = i;
            dataB[i] = i * 2f;
        }

        // 2. Выделяем память на GPU под эти массивы (ComputeBuffer)
        // Параметры: (количество_элементов, размер_одного_элемента_в_байтах)
        // float занимает 4 байта.
        ComputeBuffer bufferA = new ComputeBuffer(arraySize, sizeof(float));
        ComputeBuffer bufferB = new ComputeBuffer(arraySize, sizeof(float));
        ComputeBuffer bufferC = new ComputeBuffer(arraySize, sizeof(float));

        // 3. Закачиваем данные из памяти CPU в память GPU
        bufferA.SetData(dataA);
        bufferB.SetData(dataB);

        // 4. Связываем буферы памяти с переменными внутри Compute-шейдера
        // Сначала находим ID функции (ядра) в шейдере
        int kernelIndex = computeShader.FindKernel("CSMain");

        computeShader.SetBuffer(kernelIndex, "bufferA", bufferA);
        computeShader.SetBuffer(kernelIndex, "bufferB", bufferB);
        computeShader.SetBuffer(kernelIndex, "bufferC", bufferC);

        // 5. ЗАПУСКАЕМ ВЫЧИСЛЕНИЯ НА GPU!
        // Нам нужно обработать 1 000 000 элементов.
        // В самом шейдере мы указали, что в одной группе [numthreads(64, 1, 1)] находится 64 потока.
        // Значит, нам нужно запустить (1 000 000 / 64) групп.
        int threadGroupsX = Mathf.CeilToInt((float)arraySize / 64f);

        // Функция Dispatch запускает вычисления на GPU. Параметры: (kernelIndex, groupsX, groupsY, groupsZ)
        computeShader.Dispatch(kernelIndex, threadGroupsX, 1, 1);

        // 6. Забираем посчитанный результат обратно с GPU в CPU (в массив dataResult)
        // ВНИМАНИЕ: Это «тяжелая» операция, так как данные идут через медленную шину PCIe!
        bufferC.GetData(dataResult);

        // Проверяем несколько элементов, чтобы убедиться, что всё посчитано верно
        Debug.Log("Элемент [5]: " + dataResult[5] + " (Ожидалось: 15)");
        Debug.Log("Элемент [100]: " + dataResult[100] + " (Ожидалось: 300)");
        Debug.Log("Элемент [999999]: " + dataResult[999999]);

        // 7. ОБЯЗАТЕЛЬНО освобождаем память на GPU, иначе будет утечка видеопамяти!
        bufferA.Release();
        bufferB.Release();
        bufferC.Release();
    }
}