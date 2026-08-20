using BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CvSectionSegmenterTests
{
    [Fact]
    public void Segment_BilinenBasliklar_BolumleriDogruAyirir()
    {
        var text = string.Join(
            '\n',
            "DENEYIM",
            "Acme AS - Yazilim Muhendisi 01/2020 - 03/2022",
            "EGITIM",
            "Ornek Universitesi - Bilgisayar Muhendisligi 2016 - 2020",
            "BECERILER",
            "C#, SQL, Azure");

        var sections = CvSectionSegmenter.Segment(text);

        var experience = Assert.Single(sections, s => s.Kind == CvSectionKind.Experience);
        Assert.Equal(["Acme AS - Yazilim Muhendisi 01/2020 - 03/2022"], experience.Entries);

        var education = Assert.Single(sections, s => s.Kind == CvSectionKind.Education);
        Assert.Equal(["Ornek Universitesi - Bilgisayar Muhendisligi 2016 - 2020"], education.Entries);

        var skills = Assert.Single(sections, s => s.Kind == CvSectionKind.Skills);
        Assert.Equal(["C#, SQL, Azure"], skills.Entries);
    }

    [Fact]
    public void Segment_TurkceKarakterliVeIkiNoktaliBaslik_Taninir()
    {
        var text = string.Join('\n', "İş Deneyimi:", "Bir satır metin.");

        var sections = CvSectionSegmenter.Segment(text);

        var experience = Assert.Single(sections);
        Assert.Equal(CvSectionKind.Experience, experience.Kind);
        Assert.Equal(["Bir satır metin."], experience.Entries);
    }

    [Fact]
    public void Segment_SertifikaBasligi_Taninir()
    {
        var text = string.Join(
            '\n',
            "SERTIFIKALAR",
            "AWS Certified Solutions Architect (2022)",
            "Scrum Master Sertifikasi (2021)");

        var sections = CvSectionSegmenter.Segment(text);

        var certifications = Assert.Single(sections, s => s.Kind == CvSectionKind.Certifications);
        Assert.Equal(
            ["AWS Certified Solutions Architect (2022)", "Scrum Master Sertifikasi (2021)"],
            certifications.Entries);
    }

    [Fact]
    public void Segment_ProjeBasligi_Taninir()
    {
        var text = string.Join('\n', "PROJELER", "Ic Portal Yenileme Projesi - Teknik Lider");

        var sections = CvSectionSegmenter.Segment(text);

        var projects = Assert.Single(sections, s => s.Kind == CvSectionKind.Projects);
        Assert.Equal(["Ic Portal Yenileme Projesi - Teknik Lider"], projects.Entries);
    }

    [Fact]
    public void Segment_BasariBasligi_Taninir()
    {
        var text = string.Join('\n', "BASARILAR", "Yilin Calisani Odulu 2021");

        var sections = CvSectionSegmenter.Segment(text);

        var achievements = Assert.Single(sections, s => s.Kind == CvSectionKind.Achievements);
        Assert.Equal(["Yilin Calisani Odulu 2021"], achievements.Entries);
    }

    [Fact]
    public void Segment_IlkBasliktanOncekiSatirlar_HicbirBolumeDahilEdilmez()
    {
        var text = string.Join('\n', "Ad Soyad", "0555 555 55 55", "DENEYIM", "Tek satir.");

        var sections = CvSectionSegmenter.Segment(text);

        var section = Assert.Single(sections);
        Assert.Equal(CvSectionKind.Experience, section.Kind);
        Assert.Equal(["Tek satir."], section.Entries);
    }

    [Fact]
    public void Segment_BosMetin_BosListeDoner()
    {
        var sections = CvSectionSegmenter.Segment(string.Empty);

        Assert.Empty(sections);
    }

    [Fact]
    public void Segment_BilinmeyenBaslikBenzeriSatir_BolumBaslatmaz()
    {
        var text = string.Join('\n', "Bu bir cumle degil ama basliksa da bilinmiyor", "Ikinci satir.");

        var sections = CvSectionSegmenter.Segment(text);

        Assert.Empty(sections);
    }
}
