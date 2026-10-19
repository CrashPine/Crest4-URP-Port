using Crest;
using NUnit.Framework;
using UnityEngine;

public class SpectrumTests
{
    [TestCase(1)]
    [TestCase(8)]
    public void SpectrumSamplingIsOrderedAndInsideOctaves(int components)
    {
        var previousRandomState = Random.state;
        var spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        try
        {
            Random.InitState(7391);
            float[] wavelengths = null, directions = null;
            spectrum.GenerateWaveData(components, ref wavelengths, ref directions);
            Assert.That(wavelengths.Length, Is.EqualTo(OceanWaveSpectrum.NUM_OCTAVES * components));
            for (int i = 0; i < wavelengths.Length; ++i)
            {
                Assert.That(float.IsNaN(wavelengths[i]) || float.IsInfinity(wavelengths[i]), Is.False);
                var minimum = OceanWaveSpectrum.SmallWavelength(i / components);
                Assert.That(wavelengths[i], Is.InRange(minimum, minimum * 2));
                if (i > 0) Assert.That(wavelengths[i], Is.GreaterThanOrEqualTo(wavelengths[i - 1]));
                Assert.That(directions[i], Is.InRange(-spectrum._waveDirectionVariance, spectrum._waveDirectionVariance));
            }
        }
        finally { Object.DestroyImmediate(spectrum); Random.state = previousRandomState; }
    }

    [Test]
    public void SpectrumGenerationIsRepeatableForFixedSeed()
    {
        var previousRandomState = Random.state;
        var spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        try
        {
            float[] first = null, second = null, firstAngles = null, secondAngles = null;
            Random.InitState(517);
            spectrum.GenerateWaveData(4, ref first, ref firstAngles);
            Random.InitState(517);
            spectrum.GenerateWaveData(4, ref second, ref secondAngles);
            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreEqual(firstAngles, secondAngles);
        }
        finally { Object.DestroyImmediate(spectrum); Random.state = previousRandomState; }
    }
}
