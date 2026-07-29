---
name: 3d-scroll-experience
description: Use this skill when building a 3D, scroll-driven, awwwards-style interactive web experience — triggers include "3D web sitesi", "interactive landing page", "Three.js site", "awwwards tarzı site", "scroll-driven deneyim", "hero animasyonu". Covers the React + Vite + react-three-fiber + GSAP ScrollTrigger stack, architecture, technical conventions, and performance rules for this project's future hero/landing/brand moments. Do NOT use for forms, tables, dashboards, or other functional/enterprise screens.
---

# 3D Scroll Experience (React Three Fiber + GSAP)

Bu skill, projenin ileride React'e geçecek **hero/landing/marka anları** için
kullanılacak 3D, scroll-driven, awwwards tarzı deneyimlerin nasıl kurulacağını
tanımlar.

## Ne zaman kullanılır

- "3D web sitesi", "interactive landing page", "Three.js site",
  "awwwards tarzı site", "scroll-driven deneyim", "hero animasyonu" gibi
  istekler geldiğinde.
- Sadece **hero, landing sayfası veya marka anları** için. Form, tablo,
  dashboard gibi işlevsel/kurumsal ekranlarda **kullanılmaz** — bu pattern
  erişilebilirlik ve "sade, profesyonel" UX hedefiyle çelişir.

## Stack

- **React + Vite**
- **@react-three/fiber** — Three.js için React renderer
- **@react-three/drei** — yardımcı bileşenler (useGLTF, useProgress, Environment, vb.)
- **@react-three/postprocessing** — bloom, efektler
- **gsap + ScrollTrigger** — scroll'a bağlı animasyon orkestrasyon
- **zustand** — scroll/sahne durumunu paylaşmak için hafif state store

## Mimari

- `<Canvas>` sayfada **fixed/pinned tam ekran katman** olarak durur (viewport'u kaplar, kendi başına scroll etmez).
- Okunabilir DOM içerik bunun **üstünde**, kendi normal akışında scroll eder (gerçek sayfa scroll'u DOM'da olur).
- GSAP **ScrollTrigger**, scroll pozisyonunu okuyup **zustand store'a veya bir ref'e** yazar.
- R3F sahnesi bu değeri **`useFrame` içinde okuyarak** kamera/obje/shader durumunu her frame günceller (React state üzerinden değil — re-render tetiklemeden, doğrudan ref/store okuma).

```
DOM (scroll akışı) --> GSAP ScrollTrigger --> zustand store / ref
                                                      |
                                                      v
                                    <Canvas> (fixed) --> useFrame okur --> sahneyi günceller
```

## Teknik detaylar

- **Kamera hareketleri**: her zaman `scrub: true` ve `ease: 'none'` ile — scroll pozisyonuyla bire bir, gecikmesiz senkron.
- **Hero arkaplanları**: düz `MeshStandardMaterial` değil, **custom GLSL shader** (noise / fresnel gibi efektlerle).
- **Bloom**: `intensity` `0.3`–`0.6` aralığında tutulur (fazlası "cheap" görünür).
- **Partiküller**: tekil mesh'ler değil, **instanced mesh** ile render edilir.
- **Metin**:
  - Okunabilir/gövde metin → **DOM + Tailwind + GSAP** (erişilebilir, seçilebilir, SEO-dostu).
  - **3D text** sadece hero/logo gibi marka anlarında kullanılır, gövde metinde kullanılmaz.
- **Loading screen**: `useProgress` (drei) ile gerçek yükleme ilerlemesi gösterilir — sahte/sabit spinner değil.

## Performans kuralları

- Modeller: **`.glb`** formatında, **Draco veya Meshopt** sıkıştırmasıyla.
- Sahne başına **1-2 dinamik ışık** ile sınırlı tutulur (fazlası mobilde/GPU'da maliyetli).
- `<Canvas dpr={[1, 1.5]}>` — retina ekranlarda gereksiz aşırı çözünürlükten kaçınmak için üst sınır.
- **Lazy-load**: önce statik/2D bir poster görsel gösterilir, 3D sahne arkada hazırlanıp sonra **hydrate** edilir.
- `prefers-reduced-motion` medya sorgusu ve `navigator.hardwareConcurrency` düşük olan cihazlar için **fallback** (daha basit sahne veya statik görsel) sağlanır.
- Component **unmount** olduğunda Three.js kaynakları (geometry, material, texture) **`dispose()`** edilir — bellek sızıntısını önlemek için.

## Önemli kısıt

Bu pattern **sadece** hero/landing/marka anlarında kullanılmalı. Form, tablo,
dashboard gibi işlevsel/kurumsal ekranlarda kullanılmamalı; bu tür ekranlarda
hedef her zaman sade ve erişilebilir, standart DOM tabanlı UX olmalı.
