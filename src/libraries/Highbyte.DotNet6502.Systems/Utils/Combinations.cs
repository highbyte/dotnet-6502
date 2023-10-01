namespace Highbyte.DotNet6502.Systems.Utils;

using System.Collections.Generic;

public static class Combinations
{
    /// <summary>
    /// Enumerate all possible m-size combinations of [0, 1, ..., n-1] array
    /// in lexicographic order (first [0, 1, 2, ..., m-1]).
    /// 
    /// Code from: https://codereview.stackexchange.com/questions/194967/get-all-combinations-of-selecting-k-elements-from-an-n-sized-array/195025#195025
    /// </summary>
    /// <param name="m"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    public static IEnumerable<int[]> CombinationsRosettaWoRecursion(int m, int n)
    {
        int[] result = new int[m];
        Stack<int> stack = new Stack<int>(m);
        stack.Push(0);
        while (stack.Count > 0)
        {
            int index = stack.Count - 1;
            int value = stack.Pop();
            while (value < n)
            {
                result[index++] = value++;
                stack.Push(value);
                if (index != m) continue;
                yield return (int[])result.Clone(); // thanks to @xanatos
                //yield return result;
                break;
            }
        }
    }
}
