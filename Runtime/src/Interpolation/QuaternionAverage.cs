using UnityEngine;

// Quaternion Dark Magic
// https://theorangeduck.com/page/quaternion-weighted-average#:~:text=Essentially%20we%20just%20keep%20a,quat_abs%20function%20if%20we%20like.
// https://www.acsu.buffalo.edu/%7Ejohnc/ave_quat07.pdf

public class QuaternionAverage
{
    // 1. Create the 4x4 matrix M (Sum of outer products)
    // We use a float array to represent the 4x4 matrix
    float[,] M = new float[4, 4];
    float[] v = new float[4];

    public QuaternionAverage()
    {
        Reset();
    }
    
    public void Reset()
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                M[i, j] = 0;
            }
        }
    }
    
    public void AccumulateRot(Quaternion rotation)
    {
        v[0] = rotation.x;
        v[1] = rotation.y;
        v[2] = rotation.z;
        v[3] = rotation.w;
            
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                M[i, j] += v[i] * v[j];
            }
        }
    }
    
    public Quaternion GetAverageRotation(int rotationCount)
    {
        // 2. Solve for the largest Eigenvector using Power Iteration
        // We normalize M by the count for stability (optional)
        float invCount = 1.0f / rotationCount;
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                M[i, j] *= invCount;

        return PowerIteration(M);
    }

    private static Quaternion PowerIteration(float[,] matrix, int iterations = 10)
    {
        // Start with a guess (Vector4.one)
        Vector4 v = new Vector4(1, 1, 1, 1).normalized;

        for (int iter = 0; iter < iterations; iter++)
        {
            float[] nextV = new float[4];
            for (int i = 0; i < 4; i++)
            {
                nextV[i] = 0;
                for (int j = 0; j < 4; j++)
                {
                    nextV[i] += matrix[i, j] * v[j];
                }
            }

            v = new Vector4(nextV[0], nextV[1], nextV[2], nextV[3]).normalized;
        }

        return new Quaternion(v.x, v.y, v.z, v.w);
    }
}