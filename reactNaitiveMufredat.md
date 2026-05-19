# React Native Mid Seviye Geliştirici Müfredatı
## React / Next.js Tecrübeli Geliştirici İçin

> **Hedef:** 12 haftada, React bilgini React Native'e taşı. Web'deki her kavramın mobilde karşılığını bilerek, gerçek bir uygulama deploy edebilen mid-level mobil geliştirici ol.
>
> **Felsefe:** React Native'i "mobilde React" değil, "React paradigmasını kullanan ama tamamen farklı bir platform" olarak öğren. JSX aynı, ama altındaki her şey farklı: rendering, styling, navigation, native API erişimi. Her konuda "React/Next.js'te nasıldı, React Native'de neden farklı?" sorusu sorulacak.
>
> **Çalışma Yöntemi:** Her faz öncesinde teorik anlatım, ardından kod. Kod örneklerinde her satırın yanında "ne yapar" + "bunu yazmasaydık ne olurdu" yorumu bulunur. Her derste React web ile tablo karşılaştırması yapılır.
>
> **Domain:** KitapApp — kitap listeleme, okuma listesi, kullanıcı profili. Web versiyonunu React/Next.js ile hayal et; mobil versiyonunu React Native ile inşa edeceğiz.

---

## DÖKÜMAN YAPISI

Her gün için ayrı bir `.md` dosyası oluşturulur. Dosya adı `gunXX_konu.md` formatındadır.

```
ReactNative/
│
├── reactNaitiveMufredat.md          ← bu dosya (yol haritası)
│
├── Faz1-Temeller/
│   ├── gun01_rn_nedir_expo_mimari.md
│   ├── gun02_core_components.md
│   ├── gun03_stylesheet_api.md
│   ├── gun04_flexbox.md
│   ├── gun05_expo_router.md
│   ├── gun06_stack_tab_navigator.md
│   ├── gun07_hafta1_ozet.md
│   ├── gun08_flatlist.md
│   ├── gun09_textinput_form.md
│   ├── gun10_platform_api.md
│   ├── gun11_safearea_dimensions.md
│   ├── gun12_modal_alert.md
│   ├── gun13_image_optimizasyon.md
│   ├── gun14_hafta2_ozet.md
│   ├── gun15_asyncstorage.md
│   ├── gun16_useeffect_yasam_dongusu.md
│   ├── gun17_zustand.md
│   ├── gun18_tanstack_query.md
│   ├── gun19_axios_api_katmani.md
│   ├── gun20_auth_flow.md
│   └── gun21_hafta3_ozet.md
│
├── Faz2-Native-ve-Animasyon/
│   ├── gun22_typescript_derinlik.md       ← YENİ
│   ├── gun23_animated_api.md
│   ├── gun24_reanimated3.md
│   ├── gun25_gesture_handler.md
│   ├── gun26_skeleton_shimmer.md
│   ├── gun27_dark_mode_theming.md
│   ├── gun28_hafta4_ozet.md
│   ├── gun29_kamera.md
│   ├── gun30_konum_harita.md
│   ├── gun31_push_notification.md
│   ├── gun32_deep_linking.md
│   ├── gun33_offline_first.md
│   ├── gun34_hafta5_ozet.md
│   ├── gun35_rhf_zod.md
│   ├── gun36_nativewind_ui_kutuphaneleri.md
│   ├── gun37_redux_toolkit.md             ← YENİ
│   ├── gun38_accessibility.md             ← YENİ
│   ├── gun39_localization_rtl.md
│   └── gun40_hafta6_ozet.md
│
├── Faz3-Performans-Test/
│   ├── gun41_js_ui_thread.md
│   ├── gun42_flatlist_optimizasyon.md
│   ├── gun43_image_cache_bundle.md
│   ├── gun44_rntl_testing.md
│   ├── gun45_hafta7_ozet.md
│   ├── gun46_eas_build.md
│   ├── gun47_ota_updates.md
│   ├── gun48_sentry.md
│   ├── gun49_firebase.md
│   ├── gun50_github_actions.md
│   ├── gun51_fastlane_maestro.md          ← YENİ
│   └── gun52_hafta8_ozet.md
│
└── Faz4-Proje/
    ├── gun53_proje_kurulum_mimari.md
    ├── gun54_onboarding_auth.md
    ├── gun55_anasayfa_liste.md
    ├── gun56_arama_filtreleme.md
    ├── gun57_kitap_detay_okuma_listesi.md
    ├── gun58_profil_ayarlar.md
    ├── gun59_animasyonlar_polish.md
    └── gun60_test_deploy.md
```

---

## PROJE YAPISI (Expo)

```
kitap-app/
├── app/                 → Expo Router sayfaları (Next.js pages/ gibi)
│   ├── (tabs)/          → Tab navigator sayfaları
│   ├── (auth)/          → Auth sayfaları
│   └── book/[id].tsx    → Dinamik rota (Next.js [id].tsx gibi)
├── components/          → Reusable component'ler
├── hooks/               → Custom hook'lar
├── services/            → API çağrıları
├── store/               → Zustand store
├── constants/           → Renkler, boyutlar, sabitler
└── assets/              → Görseller, fontlar
```

---

## GENEL BAKIŞ — 4 FAZLI YOL HARİTASI

| Faz | Hafta | Gün | Konu |
|-----|-------|-----|------|
| 1 | 1–3 | 1–21 | Temeller, Styling, Navigation, State, API, Auth |
| 2 | 4–6 | 22–40 | TypeScript, Animasyon, Native Özellikler, Form, RTK, A11y |
| 3 | 7–8 | 41–52 | Performans, Test, EAS Build, CI/CD, Fastlane, Deploy |
| 4 | 9–12 | 53–60 | KitapApp — tam proje |

---

# FAZ 1 — React Native Temelleri

> React'ı biliyorsun — JSX, component, props, state. Ama React Native'de `<div>` yok, CSS yok, `window` yok, DOM yok. Bu faz: "web'de alıştığım her şeyin mobilde karşılığı ne?" sorusunu yanıtlar.

---

## Hafta 1 — Platform, Ortam ve Core Components

### Gün 1 — React Native Nedir? Expo vs CLI, Bridge vs JSI

**Teorik:**
- React Native'in mimarisi: JS Thread + UI Thread + Native Thread
- Eski mimari: Bridge — JSON serialization ile async iletişim, neden yavaş?
- Yeni mimari (0.74+): JSI (JavaScript Interface) — senkron, doğrudan native çağrı, varsayılan
- Fabric (yeni renderer) ve TurboModules — ne değişti?
- Expo nedir? Managed Workflow vs Bare Workflow farkı
- `expo-go`: telefondan tarama → anında çalıştır (web'deki browser gibi)
- EAS (Expo Application Services): build, submit, update
- Metro Bundler — Webpack'in React Native karşılığı, farkları

**React/Next.js ile karşılaştırma:**

| Kavram | React (Web) | React Native |
|--------|-------------|--------------|
| Renderer | ReactDOM → gerçek DOM | Fabric → Native View |
| Bundler | Webpack / Turbopack | Metro |
| Styling | CSS / Tailwind | StyleSheet API |
| Dev ortamı | Browser | Expo Go / Simulator |
| Dağıtım | CDN, hosting | App Store, Play Store |
| Hot reload | Fast Refresh | Fast Refresh (aynı) |

**Kontrol Soruları:**
1. Bridge mimarisinde performans neden sorunluydu? JSI ne çözdü?
2. Expo Managed Workflow ne zaman yeterli, ne zaman Bare Workflow gerekir?
3. Metro ile Webpack arasında nasıl bir fark var?

---

### Gün 2 — Core Components: `<div>` Yok, `<View>` Var

**Teorik:**
- React Native'de HTML tag'ları **yoktur** — her şey native component
- `<View>` → `<div>` (ama CSS değil, StyleSheet)
- `<Text>` → `<p>`, `<span>`, `<h1>` hepsi (metin her zaman Text içinde olmalı)
- `<Image>` → `<img>` (ama `source={{ uri: '' }}` syntax'ı farklı)
- `<TextInput>` → `<input type="text">`
- `<TouchableOpacity>` / `<Pressable>` → `<button>`
- `<ScrollView>` → `<div style={{ overflow: 'scroll' }}`
- `<FlatList>` → virtual scroll — web'de `react-window` karşılığı
- `<SafeAreaView>` — notch, home indicator için güvenli alan (web'de yok)

**React/Next.js ile karşılaştırma:**

| Web | React Native | Fark |
|-----|-------------|------|
| `<div>` | `<View>` | View'ın children'ı flex by default |
| `<p>`, `<span>` | `<Text>` | Metin SADECE Text içinde olabilir |
| `<img src="">` | `<Image source={{ uri: "" }}>` | Ağ görseli için width/height zorunlu |
| `<button>` | `<Pressable>` / `<TouchableOpacity>` | onClick → onPress |
| `<input>` | `<TextInput>` | value + onChangeText pattern |
| `<ul><li>` | `<FlatList>` | Virtual — sadece ekrandakiler render edilir |

**Kod: İlk Ekran**
```tsx
// components/KitapKart.tsx
import { View, Text, Image, Pressable, StyleSheet } from 'react-native';

type Props = {
  baslik: string;       // kitap başlığı
  yazar: string;        // yazar adı
  kapak: string;        // kapak görseli URL
  onPress: () => void;  // karta tıklandığında — web'de onClick
};

export function KitapKart({ baslik, yazar, kapak, onPress }: Props) {
  return (
    // View: web'deki <div> — ama flex direction default column değil, column zaten
    <Pressable onPress={onPress} style={styles.kart}>
      {/* Image: width/height zorunlu — ağ görselinde RN boyutu bilemez */}
      <Image source={{ uri: kapak }} style={styles.kapak} />
      {/* Text: metin SADECE Text içinde — View içinde doğrudan string = hata */}
      <Text style={styles.baslik}>{baslik}</Text>
      <Text style={styles.yazar}>{yazar}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  // StyleSheet.create: obje freeze eder + native tarafta optimize eder
  // Bunu yazmasaydık: inline style obje her render'da yeniden oluşturulur
  kart: {
    padding: 12,           // px yok — density-independent pixels (dp)
    marginBottom: 8,
    backgroundColor: '#fff',
    borderRadius: 8,
  },
  kapak: {
    width: '100%',         // yüzde destekli
    height: 200,           // sayı: dp cinsinden
    borderRadius: 4,
  },
  baslik: {
    fontSize: 16,
    fontWeight: 'bold',    // string — CSS'teki 700 değil
    marginTop: 8,
  },
  yazar: {
    fontSize: 14,
    color: '#666',
  },
});
```

**Kontrol Soruları:**
1. `<Text>` dışında neden string yazılamaz? DOM'da bu kısıt neden yok?
2. `Image` için width/height neden zorunlu?
3. `StyleSheet.create` ile inline `style={{ }}` arasındaki performans farkı ne?

---

### Gün 3 — StyleSheet API: CSS Yok, Ama Flexbox Var

**Teorik:**
- React Native'de CSS yoktur — `StyleSheet` API kullanılır
- Sadece inline stiller desteklenir: class, id, pseudo-class (`:hover`, `:focus`) yok
- Birimler: `px` yok — sayılar dp (density-independent pixels) cinsinden
- `%` desteklenir, ama yalnızca bazı özellikler için
- Desteklenmeyen CSS özellikleri: `display: grid`, `position: fixed`, `z-index` kısıtlı
- `StyleSheet.create` vs inline style: frozen object, bridge'e bir kez gönderilir
- Platform-specific style: `Platform.OS === 'ios'` kontrolü
- Responsive: `Dimensions.get('window')` veya `useWindowDimensions` hook

**React/Next.js ile karşılaştırma:**

| CSS (Web) | StyleSheet (RN) | Fark |
|-----------|-----------------|------|
| `class`, `id` | Yok | Sadece inline style veya StyleSheet |
| `px`, `rem`, `em` | Sayı (dp) | `fontSize: 16` = 16dp |
| `:hover`, `:focus` | Yok | `onPressIn`, `onFocus` event'leri |
| `display: grid` | Yok | Sadece Flexbox |
| `position: fixed` | Yok | Absolute en yakın positioned parent'a göre |
| Tailwind class | StyleSheet object | NativeWind ile Tailwind syntax mümkün |
| CSS variables | JS constants | `COLORS.primary` şeklinde JS object |

**Kontrol Soruları:**
1. Web'de `rem` kullanıyorduk — RN'de font scaling nasıl yapılır?
2. `position: fixed` neden yok? Navigation bar'ı nasıl sabit tutarsın?
3. iOS shadow vs Android elevation farkı neden var?

---

### Gün 4 — Flexbox: Web ile Farklar

**Teorik:**
- React Native'de **sadece Flexbox** vardır — grid yok, float yok
- Büyük fark: `flexDirection` default değeri `'column'` (web'de `'row'`)
- `flex: 1` → mevcut alanı doldur
- `alignItems` default: `'stretch'`
- `justifyContent` default: `'flex-start'`
- `gap` RN 0.71'de eklendi
- `position: 'absolute'` — en yakın View'a göre, `document` değil
- `flex: 1` ile `width: '100%'` farkı: flex parent gerektirir

**React/Next.js ile karşılaştırma:**

| Web Flexbox | RN Flexbox | Fark |
|------------|------------|------|
| `flex-direction: row` (default) | `flexDirection: 'column'` (default) | En önemli fark! |
| `display: flex` gerekir | Her View zaten flex | Ayrıca yazmak gerekmez |
| `flex: 1 1 0%` | `flex: 1` | Daha basit |
| `gap: 8px` | `gap: 8` (0.71+) | Eski sürümlerde yok |
| `position: fixed` | Yok | SafeAreaView kullan |

**Kontrol Soruları:**
1. `flexDirection` default'u neden farklı? Mobil layout paradigması ne?
2. `flex: 1` çalışmıyor — parent'a ne eklemelisin?
3. 2 sütunlu grid'i Flexbox ile nasıl yaparsın?

---

### Gün 5 — Expo Router: Next.js'ten Tanıdık File-based Routing

**Teorik:**
- Expo Router: Next.js `app/` directory gibi — dosya adı = route
- `app/index.tsx` → `/` (ana ekran)
- `app/book/[id].tsx` → `/book/:id` (dinamik route)
- `app/(tabs)/` → Tab Navigator (layout file ile)
- `app/(auth)/` → Auth grubu (URL'de görünmez, layout farklı)
- `_layout.tsx` → `layout.tsx` gibi — her klasörün layout'u
- `Link` component → Next.js `Link` ile aynı syntax
- `useRouter`, `useLocalSearchParams` → Next.js `useRouter`, `useSearchParams`

**React/Next.js ile karşılaştırma:**

| Next.js (App Router) | Expo Router | Fark |
|---------------------|-------------|------|
| `app/page.tsx` | `app/index.tsx` | Dosya adı farklı |
| `app/book/[id]/page.tsx` | `app/book/[id].tsx` | Klasör yerine dosya |
| `layout.tsx` | `_layout.tsx` | Alt çizgi prefix |
| `<Link href="/book/1">` | `<Link href="/book/1">` | Aynı! |
| `useSearchParams()` | `useLocalSearchParams()` | Local = bu ekranın params'ı |
| `router.push('/book/1')` | `router.push('/book/1')` | Aynı! |

**Kontrol Soruları:**
1. `(tabs)` klasörü neden parantez içinde? URL'ye yansır mı?
2. Expo Router ile React Navigation arasındaki fark ne?
3. `router.replace` ile `router.push` farkı — auth redirect'te hangisi?

---

### Gün 6 — Stack ve Tab Navigator ile Gerçek Navigasyon

**Teorik:**
- Mobile navigation web'den tamamen farklı — "history" yerine "stack"
- Stack Navigator: sayfalar üst üste yığılır — geri = pop
- Tab Navigator: alt tab bar — web'deki sekme gibi ama native
- Drawer Navigator: yan menü
- `goBack()` → browser'daki `history.back()` benzeri ama stack'ten pop
- Header: native top bar — web'de yoktu, mobilde standart
- Tab bar icon: `@expo/vector-icons` — Material Icons, Ionicons
- `initialRouteName`: hangi tab/screen başlangıçta açık?
- Deep link: URL ile doğrudan belirli bir sayfaya git

**Kontrol Soruları:**
1. Stack Navigator'da "geri" nasıl çalışır? Web history ile fark ne?
2. Tab Navigator'da bir tab'a her geçişte component yeniden mount mu olur?
3. Web'de URL ile sayfalar arası parametre taşıdık — mobilde en iyi yol ne?

---

### Gün 7 — Hafta 1 Özeti & Mini Proje: Kitap Listesi Ekranı

**Mini Proje:** Tab bar (Anasayfa, Ara, Profil), header, FlatList ile 10 kitap kartı, karta basınca detay sayfasına git.

**Hafta 1 Özet Tablosu:**

| Sen web'de ne yapıyordun | Mobilde nasıl yapılır |
|--------------------------|----------------------|
| `<div className="flex">` | `<View style={{ flex: 1 }}>` |
| Tailwind `text-lg font-bold` | `StyleSheet: { fontSize: 18, fontWeight: 'bold' }` |
| `<Link href="/book/1">` | `<Link href="/book/1">` (aynı!) |
| `useRouter().push()` | `useRouter().push()` (aynı!) |
| Browser back button | Native back gesture / button |
| `react-router` layout | `_layout.tsx` |

---

## Hafta 2 — Liste, Form ve Platform Farkları

### Gün 8 — FlatList: Virtual Scroll ve Performans

**Teorik:**
- `ScrollView`: tüm children'ı bir anda render eder — az item için uygun
- `FlatList`: sadece ekranda görünen item'ları render eder — büyük listeler için
- Web'deki `react-window` / `react-virtualized` ile aynı mantık
- `keyExtractor`: her item'ın unique key'i — React'taki `key` prop benzeri
- `renderItem`: her item için render fonksiyonu
- `onEndReached` + `onEndReachedThreshold`: infinite scroll
- `ListHeaderComponent`, `ListFooterComponent`, `ListEmptyComponent`
- `getItemLayout`: item yüksekliği sabit ise performans optimizasyonu
- `SectionList`: gruplu liste

**React/Next.js ile karşılaştırma:**

| Web | React Native | Fark |
|-----|-------------|------|
| `items.map(item => <div>)` | `<FlatList data={items} renderItem={...}>` | FlatList virtualize eder |
| `react-window` | `FlatList` (built-in) | Ayrı kütüphane gereksiz |
| `key={item.id}` | `keyExtractor={(item) => item.id}` | Prop yerine fonksiyon |
| Infinite scroll: IntersectionObserver | `onEndReached` | Daha basit API |

**Kontrol Soruları:**
1. 1000 item için `ScrollView` vs `FlatList` — fark ne?
2. `keyExtractor` React'taki `key` prop'undan nasıl farklı?
3. `onEndReachedThreshold={0.5}` ne anlama gelir?

---

### Gün 9 — TextInput ve Form Yönetimi

**Teorik:**
- `TextInput` → HTML `<input>` — ama davranışlar farklı
- `value` + `onChangeText` — controlled input
- `keyboardType`: 'numeric', 'email-address', 'phone-pad'
- `returnKeyType`: 'done', 'next', 'search'
- `secureTextEntry`: şifre alanı
- Klavye yukarı çıkınca layout kayması: `KeyboardAvoidingView`
- `ref` + `focus()`: bir sonraki input'a geç

**React/Next.js ile karşılaştırma:**

| HTML Input | TextInput (RN) | Fark |
|-----------|----------------|------|
| `type="email"` | `keyboardType="email-address"` | Attribute adı farklı |
| `type="password"` | `secureTextEntry={true}` | Boolean prop |
| `onChange={e => setValue(e.target.value)}` | `onChangeText={setValue}` | Direkt string, event yok |
| Klavye overlap: CSS `position: fixed` | `KeyboardAvoidingView` | Native çözüm gerekir |

**Kontrol Soruları:**
1. `onChange` yerine `onChangeText` — neden event objesi yok?
2. `KeyboardAvoidingView` neden gerekli? Web'de bu sorun neden yok?
3. iOS'ta `behavior="padding"`, Android'de `behavior="height"` — neden farklı?

---

### Gün 10 — Platform API: iOS vs Android Farkları

**Teorik:**
- `Platform.OS`: 'ios' | 'android' | 'web'
- `Platform.select({ ios: ..., android: ..., default: ... })`
- Platform-specific dosyalar: `Button.ios.tsx` ve `Button.android.tsx`
- iOS: safe area (notch, Dynamic Island), Android: status bar rengi
- `StatusBar`: rengi, stilini ayarla
- `SafeAreaView` ve `useSafeAreaInsets`
- Haptic feedback: iOS'ta `expo-haptics`
- Back button: Android'de fiziksel geri tuşu — `BackHandler` API
- Permissions: iOS önce sor → reddedilince Settings'e yönlendir; Android runtime permission

**Kontrol Soruları:**
1. `Platform.select` vs `Platform.OS === 'ios'` — ne zaman hangisi?
2. Platform-specific dosya (.ios.tsx) ne zaman kullanmalısın?
3. Android back button — web'deki browser back ile aynı mı davranır?

---

### Gün 11 — SafeAreaView, Dimensions ve Responsive Layout

**Teorik:**
- `SafeAreaView`: notch, Dynamic Island, home indicator için padding
- `useSafeAreaInsets()`: exact inset değerleri
- `Dimensions.get('window')`: ekran boyutu
- `useWindowDimensions()`: hook versiyonu, orientation change'de güncellenir
- Responsive tasarım: web'deki media query yok — JS ile kontrol
- `PixelRatio`: cihazın pixel density'si

**React/Next.js ile karşılaştırma:**

| Web | React Native | Fark |
|-----|-------------|------|
| `@media (max-width: 768px)` | `width < 768 ? ... : ...` (JS) | Media query yok, JS ile |
| `100vh` | `Dimensions.get('window').height` | Hook: `useWindowDimensions` |
| `env(safe-area-inset-top)` | `useSafeAreaInsets().top` | Benzer mantık |

**Kontrol Soruları:**
1. `SafeAreaView` yerine `useSafeAreaInsets` ne zaman kullanırsın?
2. Orientation change'de `Dimensions.get` neden güncel değer vermeyebilir?
3. 1dp = kaç pixel? Thin border (1px) nasıl çizilir?

---

### Gün 12 — Modal, Alert ve ActionSheet

**Teorik:**
- `Alert.alert()`: native alert dialog
- `Alert.alert(title, message, buttons)`: confirm dialog
- `Modal`: custom modal — `visible`, `animationType`, `transparent`, `onRequestClose`
- `onRequestClose`: Android back button'a basınca çağrılır
- ActionSheet: iOS'ta native bottom sheet seçenek listesi
- Bottom Sheet: `@gorhom/bottom-sheet`

**Kontrol Soruları:**
1. Web'de `window.confirm()` sync — `Alert.alert()` neden async/callback?
2. `Modal`'da `onRequestClose` neden önemli? Android'de olmazsa ne olur?
3. Bottom Sheet vs Modal — ne zaman hangisi daha iyi UX?

---

### Gün 13 — Image Optimizasyonu ve Vektör İkonlar

**Teorik:**
- `<Image>`: network, local (require), base64
- `resizeMode`: 'cover', 'contain', 'stretch' — CSS `object-fit` benzeri
- `expo-image`: performanslı, cached, blurhash placeholder — Next.js `<Image>` benzeri
- `@expo/vector-icons`: 1000+ ikon seti
- `react-native-svg` + `SvgUri`: SVG görselleri

**React/Next.js ile karşılaştırma:**

| Next.js Image | expo-image / RN Image | Fark |
|---------------|----------------------|------|
| `<Image src="" alt="">` | `<Image source={{ uri: '' }}>` | `alt` yok, `accessibilityLabel` var |
| Otomatik lazy load | `lazy` prop (expo-image) | Varsayılan değil |
| `placeholder="blur"` | `placeholder={{ blurhash: '...' }}` | expo-image destekler |

**Kontrol Soruları:**
1. `expo-image` ile built-in `Image` arasındaki fark ne?
2. Ağ görseli için neden width/height zorunlu?
3. SVG'yi React Native'de nasıl kullanırsın?

---

### Gün 14 — Hafta 2 Özeti & Mini Proje: Login Ekranı

**Mini Proje:** Login ekranı: email + şifre TextInput, login butonu, KeyboardAvoidingView, Alert ile hata mesajı, başarılı girişte ana sayfaya yönlendir.

---

## Hafta 3 — State, Hooks ve Veri Yönetimi

### Gün 15 — AsyncStorage: localStorage'ın Karşılığı

**Teorik:**
- Web'de: `localStorage.setItem('key', JSON.stringify(value))`
- React Native'de: `@react-native-async-storage/async-storage`
- Fark: AsyncStorage **asenkron** (Promise tabanlı) — localStorage sync
- `SecureStore` (`expo-secure-store`): şifreli depolama — token saklamak için
- `MMKV` (`react-native-mmkv`): sync, hızlı — Zustand persist için ideal
- `react-native-keychain`: biyometrik auth destekli güvenli depolama (SecureStore'dan daha güçlü)

**React/Next.js ile karşılaştırma:**

| Web | React Native | Fark |
|-----|-------------|------|
| `localStorage` (sync) | `AsyncStorage` (async, Promise) | `await` gerekir |
| `sessionStorage` | Yok | — |
| `cookie` (HttpOnly) | `SecureStore` / `Keychain` | Güvenli depolama |

**Kontrol Soruları:**
1. `localStorage` sync iken `AsyncStorage` neden async?
2. JWT token'ı nerede saklamalısın? AsyncStorage, SecureStore, Keychain — farkları?
3. `MMKV` ile `AsyncStorage` arasındaki hız farkı neden bu kadar büyük?

---

### Gün 16 — useEffect ve React Native Yaşam Döngüsü

**Teorik:**
- `useEffect`: React Native'de aynı çalışır
- `AppState`: foreground, background, inactive
- `useFocusEffect` (Expo Router): ekrana her gelince çalış
- `useEffect` vs `useFocusEffect` farkı
- Memory leak: unmount'ta cleanup

**Kod:**
```tsx
import { AppState, AppStateStatus } from 'react-native';
import { useFocusEffect } from 'expo-router';
import { useCallback } from 'react';

// Ekrana her gelince veriyi tazele
// useEffect ile yazılsaydı: sadece ilk mount'ta çalışırdı
useFocusEffect(
  useCallback(() => {
    // useCallback olmadan: her render'da yeni fonksiyon → sonsuz döngü
    kitaplariYukle();
    return () => { /* cleanup */ };
  }, [])
);

useEffect(() => {
  const subscription = AppState.addEventListener('change', (state: AppStateStatus) => {
    if (state === 'active') tokenKontrolEt();
  });
  return () => subscription.remove(); // bunu yazmasaydık: memory leak
}, []);
```

**Kontrol Soruları:**
1. `useEffect` ile `useFocusEffect` farkı?
2. `AppState` 'background' ne zaman tetiklenir?
3. `useFocusEffect` içinde `useCallback` neden zorunlu?

---

### Gün 17 — Zustand ile State Management

**Teorik:**
- Zustand: minimal, hook tabanlı state management
- `persist` middleware + `MMKV` storage
- `immer` middleware
- Selector optimizasyonu
- Slice pattern: auth slice, kitap slice ayrı

**Kod:**
```tsx
// store/kitapStore.ts
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

export const useKitapStore = create<KitapStore>()(
  persist(
    (set) => ({
      okumalistesi: [],
      listeEkle: (id) =>
        set((state) => ({
          okumalistesi: [...state.okumalistesi, id], // bunu yazmasaydık: listeyi kaybederdik
        })),
      listeCikar: (id) =>
        set((state) => ({
          okumalistesi: state.okumalistesi.filter((k) => k !== id),
        })),
    }),
    {
      name: 'kitap-store',
      storage: createJSONStorage(() => storage), // MMKV adapter
    }
  )
);
```

**Kontrol Soruları:**
1. Context API vs Zustand — ne zaman Context yeterli?
2. `persist` olmadan store kapatılınca ne olur?
3. Selector kullanmadan tüm store alınırsa performans farkı ne?

---

### Gün 18 — TanStack Query: API Veri Yönetimi

**Teorik:**
- `@tanstack/react-query`: web'de ne kullanıyordunsa aynı — tam uyumlu
- `useQuery`, `useMutation`, `useInfiniteQuery`
- `refetchOnWindowFocus` → mobilde false olmalı (window focus yok)
- `refetchOnReconnect`: internet gelince otomatik yenile
- Offline support: `networkMode: 'offlineFirst'`

**React/Next.js ile karşılaştırma:**

| Next.js | React Native | Fark |
|---------|-------------|------|
| `refetchOnWindowFocus: true` | `refetchOnWindowFocus: false` | Window focus yok |
| Network: online → refetch | `refetchOnReconnect: true` | Mobilde daha kritik |
| SSR: `dehydrate/hydrate` | Yok | RN'de SSR yok |

**Kontrol Soruları:**
1. `refetchOnWindowFocus` mobilde neden false olmalı?
2. Offline modda `useQuery` ne döner?
3. Infinite scroll: `useInfiniteQuery` ile `FlatList onEndReached` nasıl entegre?

---

### Gün 19 — Axios ve API Katmanı

**Teorik:**
- Axios React Native'de çalışır — kurulum aynı
- `interceptors`: token ekleme, 401 → refresh → retry
- `baseURL` ortam bazlı: `expo-constants` ile
- Timeout: mobilde ağ yavaş — 10s öner
- Network durumu: `@react-native-community/netinfo`

**Kod:**
```tsx
// services/api.ts
import axios from 'axios';
import * as SecureStore from 'expo-secure-store';

const api = axios.create({
  baseURL: Constants.expoConfig?.extra?.apiUrl,
  timeout: 10_000,    // 10 saniye — mobilde ağ kesintisi normaldir
});

api.interceptors.request.use(async (config) => {
  const token = await SecureStore.getItemAsync('access_token');
  // bunu yazmasaydık: her istekte token manuel eklenmesi gerekirdi
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await tokenYenile();
      return api(error.config); // başarısız isteği tekrar dene
    }
    return Promise.reject(error);
  }
);
```

**Kontrol Soruları:**
1. Web'de `localStorage`'dan token aldık, mobilde neden `SecureStore`?
2. `timeout: 10_000` neden önemli?
3. `netinfo` ile Axios nasıl entegre edilir?

---

### Gün 20 — Authentication Flow ve Korumalı Rotalar

**Teorik:**
- Expo Router'da auth flow: `(auth)` grubu + root layout'ta yönlendirme
- Next.js'teki `middleware.ts` ile benzer ama farklı — client-side
- `SplashScreen.preventAutoHideAsync()`: hazır olana kadar splash göster
- Korumalı rota: token yoksa `(auth)/login`'e yönlendir
- Deep link + auth: link tıklandı ama kullanıcı giriş yapmamış

**React/Next.js ile karşılaştırma:**

| Next.js Auth | Expo Router Auth | Fark |
|-------------|-----------------|------|
| `middleware.ts` — server-side | `useEffect` + `router.replace()` — client-side | RN'de server yok |
| `cookies()` ile token kontrol | `SecureStore.getItemAsync()` | Storage farklı |
| `redirect('/login')` (server) | `router.replace('/(auth)/login')` | Client-side, async |

**Kontrol Soruları:**
1. `router.push` vs `router.replace` — auth redirect'te neden `replace`?
2. `SplashScreen.preventAutoHideAsync()` neden gerekli?
3. Deep link + unauthenticated user — akışı nasıl tasarlarsın?

---

### Gün 21 — Hafta 3 Özeti & Mini Proje: Tam Auth + Kitap Listeleme

**Mini Proje:** Login/register akışı, JWT token SecureStore'da, korumalı tab navigator, TanStack Query ile kitap listesi API'den, Zustand ile okuma listesi, offline cache.

---

# FAZ 2 — Native Özellikler, Animasyon ve İleri Konular

---

## Hafta 4 — TypeScript Derinliği ve Animasyon

### Gün 22 — TypeScript Derinliği: Navigation Params, Generics, Utility Types ★ YENİ

**Teorik:**
- React Native'de TypeScript neden özellikle kritik? Native bridge tip güvencesi
- `strict: true` — job posting'lerin tamamında bekleniyor, `any` yazmak mid-level'ı junior'a düşürür
- Navigation params type'lama: Expo Router ile `useLocalSearchParams<{ id: string }>()`
- Stack screen params typing: `RootStackParamList` tanımı
- Generic component'ler: `FlatList<Kitap>`, `useQuery<Kitap[]>`
- Utility types: `Partial<T>`, `Pick<T, K>`, `Omit<T, K>`, `ReturnType<F>`, `Awaited<T>`
- `as const` — literal type inference
- Discriminated union: API response tipi (`{ status: 'ok', data: T } | { status: 'error', message: string }`)
- `zod` ile runtime + compile-time tip güvencesi birlikte

**React/Next.js ile karşılaştırma:**

| Web TypeScript | React Native TypeScript | Ekstra |
|---------------|------------------------|--------|
| `useParams<{ id: string }>()` | `useLocalSearchParams<{ id: string }>()` | Aynı mantık |
| `useState<User \| null>(null)` | Aynı | Aynı |
| Event tipi: `React.ChangeEvent<HTMLInputElement>` | `onChangeText: (text: string) => void` | Event wrapper yok |
| `StyleSheet.create` dönüş tipi | `StyleSheet.NamedStyles<T>` | Stilini type etmek |

**Kod:**
```tsx
// 1. Discriminated Union — API response
type ApiResponse<T> =
  | { status: 'ok'; data: T }
  | { status: 'error'; message: string };
// bunu yazmasaydık: her yerde if(response.data) kontrolü güvenilmez olurdu

// 2. Utility Types
type Kitap = {
  id: string;
  baslik: string;
  yazar: string;
  kapak: string;
  aciklama: string;
};

type KitapOnizleme = Pick<Kitap, 'id' | 'baslik' | 'kapak'>;
// Pick: sadece belirtilen field'ları al — FlatList'te tam Kitap göndermek gerekmez
// bunu yazmasaydık: tüm Kitap objesini prop olarak geçirirdik, gereksiz veri

type KitapGuncelle = Partial<Omit<Kitap, 'id'>>;
// Omit: id hariç al, Partial: hepsini optional yap — PATCH request tipi
// bunu yazmasaydık: güncelleme formunda tüm alanlar zorunlu olurdu

// 3. Generic hook
function useLocalStorage<T>(key: string, defaultValue: T) {
  // T: ne tür veri saklayacağını çağırırken belirtirsin
  // bunu yazmasaydık: her hook için ayrı ayrı yazardık
}

// 4. Navigation params — Expo Router
import { useLocalSearchParams } from 'expo-router';

export default function BookDetail() {
  const { id, tab } = useLocalSearchParams<{ id: string; tab?: string }>();
  // tab?: string → optional param
  // bunu yazmasaydık: id ve tab string | string[] dönebilir, tip hataları olurdu
}

// 5. as const — sabit değerler için
const TABS = ['anasayfa', 'arama', 'profil'] as const;
type Tab = typeof TABS[number]; // 'anasayfa' | 'arama' | 'profil'
// bunu yazmasaydık: Tab = string olurdu, yazım hatalarını compile'da yakalayamazdık
```

**Kontrol Soruları:**
1. `Pick` ile `Omit` farkı ne? Her ikisini ne zaman kullanırsın?
2. `as const` olmadan `TABS[number]` neden `string` döner?
3. Discriminated union, `status` field'ı olmadan nasıl yazılabilirdi? Neden daha zor?
4. `Awaited<ReturnType<typeof kitapGetir>>` ne anlama gelir?

---

### Gün 23 — Animated API: JavaScript Thread Animasyonları

**Teorik:**
- `Animated` API: React Native'in built-in animasyon sistemi
- JS thread üzerinde çalışır → UI thread ile asenkron → jank riski
- `Animated.Value`: animasyonlu değer tutar
- `Animated.timing`, `Animated.spring`, `Animated.sequence`, `Animated.parallel`
- `useNativeDriver: true`: animasyonu native thread'e taşı — sadece transform ve opacity
- CSS ile karşılaştırma: CSS animation/transition vs JS driven animation

**React/Next.js ile karşılaştırma:**

| Web CSS | Animated API | Fark |
|---------|-------------|------|
| `transition: opacity 0.3s` | `Animated.timing(opacity, { toValue: 0, duration: 300 })` | Imperative |
| `@keyframes` | `Animated.sequence` | Farklı paradigma |
| GPU hızlandırma: `transform` | `useNativeDriver: true` (sadece transform/opacity) | Sınırlı |

**Kod:**
```tsx
export function FadeInView({ children }: { children: React.ReactNode }) {
  const opacity = useRef(new Animated.Value(0)).current;
  // useRef: değer her render'da sıfırlanmasın
  // bunu yazmasaydık: useState ile yazılsaydı re-render tetiklenirdi

  useEffect(() => {
    Animated.timing(opacity, {
      toValue: 1,
      duration: 500,
      useNativeDriver: true, // bunu yazmasaydık: JS thread, jank olabilir
    }).start();
  }, []);

  return <Animated.View style={{ opacity }}>{children}</Animated.View>;
}
```

**Kontrol Soruları:**
1. `useNativeDriver: true` neden her zaman kullanılmaz? Kısıtları ne?
2. `Animated.spring` ile `Animated.timing` farkı?
3. JS thread animasyonu ile native thread animasyonu görsel farkı ne zaman hissedilir?

---

### Gün 24 — Reanimated 3: Native Thread Animasyonları

**Teorik:**
- `react-native-reanimated`: animasyonları tamamen native thread'de çalıştır
- `useSharedValue`: native thread'de yaşayan değer
- `useAnimatedStyle`: native'de hesaplanan stil
- `withTiming`, `withSpring`, `withDelay`, `withSequence`
- `worklet`: native thread'de çalışan fonksiyon — JS thread'e bridge yok
- Neden daha performanslı? JS thread meşgul olsa bile animasyon akmaya devam eder

**React/Next.js ile karşılaştırma:**

| Framer Motion (Web) | Reanimated 3 | Benzerlik |
|--------------------|-------------|-----------|
| `motion.div` | `Animated.View` (reanimated) | Benzer |
| `animate={{ opacity: 1 }}` | `useAnimatedStyle(() => ({ opacity: sv.value }))` | Farklı API |
| `whileTap` | `useAnimatedGestureHandler` | Benzer konsept |
| `AnimatePresence` | `Exiting`/`Entering` preset | Benzer |

**Kontrol Soruları:**
1. `worklet` direktifi ne anlama geliyor? Neden yazılıyor?
2. `Animated` API ile Reanimated — küçük projede hangisi?
3. Scroll'da header küçülmesi — Reanimated ile nasıl yapılır?

---

### Gün 25 — Gesture Handler: Touch ve Swipe

**Teorik:**
- `react-native-gesture-handler`: native gesture tanıma
- `PanGesture`, `TapGesture`, `LongPressGesture`, `PinchGesture`
- `SwipeableRow`: liste item'ı swipe → sil/arşivle
- `GestureDetector` + Reanimated: kombinasyon
- Gesture çakışması: `simultaneousWithExternalGesture`, `blocksExternalGesture`

**Kontrol Soruları:**
1. `TouchableOpacity` ile `GestureDetector + TapGesture` farkı ne?
2. FlatList scroll + item swipe gesture çakışması nasıl çözülür?
3. Web'de `onMouseDown` + `onMouseMove` ile sürükleme yaptık — mobilde fark ne?

---

### Gün 26 — Skeleton Loading ve Shimmer Efekti

**Teorik:**
- Skeleton loading: veri gelene kadar placeholder göster
- `expo-linear-gradient` + animasyon → shimmer efekti
- `ActivityIndicator`: platform'a göre loading spinner
- `blurhash` ile expo-image: görsel yüklenmeden önce blur preview

**Kontrol Soruları:**
1. `ActivityIndicator` vs skeleton loading — UX açısından hangisi ne zaman?
2. `blurhash` nasıl üretilir? Backend mi üretmeli?
3. Shimmer animasyonunu `useNativeDriver: true` ile kullanabilir misin?

---

### Gün 27 — Dark Mode ve Theming

**Teorik:**
- `useColorScheme()`: sistem dark/light mode'unu oku
- React Navigation theming: `DarkTheme`, `DefaultTheme`
- Custom theme context
- `Appearance.getColorScheme()`: hook dışında kullan
- StatusBar rengi: dark mode'da beyaz ikon
- AsyncStorage ile kullanıcı tercihi kaydet

**React/Next.js ile karşılaştırma:**

| Next.js (next-themes) | React Native | Fark |
|----------------------|-------------|------|
| `useTheme()` | Custom hook + `useColorScheme()` | Kütüphane desteği sınırlı |
| CSS variables | JS theme object | CSS yok |
| SSR + hydration | Yok | RN'de SSR yok |

**Kontrol Soruları:**
1. `useColorScheme()` ne döner? Sistem temasını kullanıcı override edebilmeli mi?
2. StatusBar'ı dark modda nasıl ayarlarsın?
3. React Navigation'da built-in dark theme var mı?

---

### Gün 28 — Hafta 4 Özeti & Proje: Animasyonlu Kitap Detay

**Mini Proje:** Shared element transition (liste → detay), shimmer loading, dark mode desteği, swipe to delete. TypeScript strict mode aktif, tüm tipler doğru yazılmış.

---

## Hafta 5 — Native Özellikler

### Gün 29 — Kamera: expo-camera

**Teorik:**
- `expo-camera`: kameraya erişim, fotoğraf/video çek
- Permission akışı: `Camera.requestCameraPermissionsAsync()`
- Permission reddedilince: kullanıcıyı Settings'e yönlendir
- `CameraView` component, `takePictureAsync()`
- `expo-image-picker`: galeri seçimi
- `expo-image-manipulator`: boyutlandır, kırp, sıkıştır
- Güvenlik: izin açıklaması plist/manifest'e eklenmeli

**React/Next.js ile karşılaştırma:**

| Web (MediaDevices API) | React Native | Fark |
|-----------------------|-------------|------|
| `navigator.mediaDevices.getUserMedia()` | `expo-camera` | Native API |
| `<video>` preview | `<CameraView>` | Native component |
| Browser permission popup | iOS/Android sistem dialog | Platform kuralları farklı |

**Kontrol Soruları:**
1. Permission reddedilince neden Settings'e yönlendirmen gerekiyor?
2. `expo-image-picker` ile `expo-camera` ne zaman hangisi?
3. Çekilen fotoğrafı API'ye upload ederken `FormData` nasıl kullanılır?

---

### Gün 30 — Konum ve Harita: expo-location + react-native-maps

**Teorik:**
- `expo-location`: konum izni + koordinat al
- `Location.requestForegroundPermissionsAsync()` vs `requestBackgroundPermissionsAsync()`
- `Location.getCurrentPositionAsync()`, `watchPositionAsync()`
- `react-native-maps`: Google Maps (Android) + Apple Maps (iOS)
- `MapView`, `Marker`, `Callout`, `Polyline`
- Geocoding: koordinat → adres

**Kontrol Soruları:**
1. Foreground vs background permission farkı ne?
2. `watchPositionAsync` memory leak için cleanup nasıl?
3. Google Maps React Native vs web versiyonu — API farkları?

---

### Gün 31 — Push Notification: Expo Notifications

**Teorik:**
- `expo-notifications`: push notification gönder/al
- Expo Push Token: cihaza özgü — backend'e kaydet
- Foreground / background notification
- `addNotificationReceivedListener`, `addNotificationResponseReceivedListener`
- Local notification: backend olmadan, belirli saatte tetikle
- iOS: permission zorunlu; Android 13+: permission gerekli

**React/Next.js ile karşılaştırma:**

| Web Push API | Expo Notifications | Fark |
|-------------|-------------------|------|
| Service Worker gerekli | Yok | RN'de SW yok |
| Browser permission popup | iOS sistem dialog | Platform bağımlı |

**Kontrol Soruları:**
1. Push token neden cihaza özel? Değişebilir mi?
2. Bildirime tıklanınca doğru sayfaya nasıl yönlendirirsin?
3. Local notification ile push notification farkı?

---

### Gün 32 — Deep Linking ve Evrensel Bağlantılar

**Teorik:**
- Deep link: `kitapapp://book/123` → uygulamada kitap detayı aç
- Universal Link (iOS) / App Link (Android): `https://` ile doğrudan uygulama
- Expo Router: deep link config `app.json` içinde
- `Linking.getInitialURL()`: uygulama link ile açıldıysa
- `Linking.addEventListener`: uygulama açıkken link gelirse
- Push notification → deep link entegrasyonu

**Kontrol Soruları:**
1. Custom scheme ile Universal Link farkı? Hangisi daha iyi UX?
2. Uygulama kapalıyken gelen deep link nasıl handle edilir?
3. Bildirime tıklanınca deep link açmak — akış nasıl kurulur?

---

### Gün 33 — Offline First ve NetInfo

**Teorik:**
- `@react-native-community/netinfo`: bağlantı durumu + tipi
- TanStack Query cache: offline'da cache'den sun
- Optimistic update: network beklemeden UI'ı güncelle, hata olursa geri al
- Sync queue: offline'da yapılan işlemleri kuyruğa al, online olunca gönder
- `MMKV` ile kalıcı cache

**React/Next.js ile karşılaştırma:**

| Web | React Native | Fark |
|-----|-------------|------|
| `navigator.onLine` | `NetInfo.fetch()` | RN'de tip bilgisi de var |
| PWA offline: Service Worker | TanStack Query cache | Farklı mekanizma |

**Kontrol Soruları:**
1. `navigator.onLine` bazen yanlış değer veriyor — NetInfo neden daha güvenilir?
2. Optimistic update başarısız olursa state nasıl geri alınır?
3. Sync queue: offline işlemleri nerede saklarsın?

---

### Gün 34 — Hafta 5 Özeti & Mini Proje: Profil + Fotoğraf + Konum

**Mini Proje:** Galeri/kamera ile profil fotoğrafı değiştir, "yakınımdaki kütüphaneler" harita ekranı, push notification izni + local bildirim test.

---

## Hafta 6 — Form, UI, Redux Toolkit ve Accessibility

### Gün 35 — React Hook Form + Zod: Web ile Aynı, Mobil Farkları

**Teorik:**
- `react-hook-form`: React Native'de çalışır
- Fark: `Controller` wrapper zorunlu (native input'lar `ref` kullanmaz)
- `zod` validasyon: web ile birebir aynı
- Klavye yönetimi: form navigation, `returnKeyType`
- `Keyboard.dismiss()`: form submit'te klavyeyi kapat

**React/Next.js ile karşılaştırma:**

| Web (RHF) | React Native (RHF) | Fark |
|-----------|-------------------|------|
| `register('email')` | `<Controller>` wrapper zorunlu | RHF ref yerine onChange/value kullanır |
| `<input {...register}>` | `<Controller render={({field}) => <TextInput ...>}` | Native component uyumu |

**Kod:**
```tsx
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

export function LoginForm() {
  const { control, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  return (
    <Controller
      control={control}
      name="email"
      render={({ field: { onChange, onBlur, value } }) => (
        <TextInput
          value={value}
          onChangeText={onChange}   // onChange değil onChangeText (RN farkı)
          // bunu yazmasaydık: her tuş basışında tip uyuşmazlığı hatası
          onBlur={onBlur}
          keyboardType="email-address"
        />
      )}
    />
  );
}
```

**Kontrol Soruları:**
1. Web'de `register()` yeterliyken RN'de neden `Controller` zorunlu?
2. `onChangeText` yerine `onChange` kullansaydın ne olurdu?
3. Form submit ederken klavyeyi nasıl kaldırırsın?

---

### Gün 36 — UI Kütüphaneleri: NativeWind ve React Native Paper

**Teorik:**
- **NativeWind**: Tailwind CSS → React Native StyleSheet dönüşümü
  - `className` prop ile Tailwind class kullan
  - Tüm utility class'lar desteklenmiyor — grid yok, position:fixed yok
- **React Native Paper**: Material Design bileşen kütüphanesi
- **Gluestack UI**: cross-platform, NativeWind ile uyumlu

**React/Next.js ile karşılaştırma:**

| Web | React Native | Benzerlik |
|-----|-------------|-----------|
| Tailwind CSS | NativeWind | Neredeyse aynı syntax |
| shadcn/ui | React Native Paper / Gluestack | Benzer konsept |
| CSS Modules | StyleSheet.create | Farklı ama benzer scope |

**Kontrol Soruları:**
1. NativeWind ile StyleSheet.create — production'da hangisi daha performanslı?
2. React Native Paper theming nasıl özelleştirilir?
3. Tailwind class'larının bir kısmı neden RN'de desteklenmiyor?

---

### Gün 37 — Redux Toolkit: Şirket Projelerinde Standart ★ YENİ

**Teorik:**
- **Neden RTK?** Job posting'lerin büyük çoğunluğu Redux / RTK istiyor — enterprise standart
- Zustand ile fark: RTK daha verbose ama daha yapılandırılmış, büyük ekiplerde tutarlılık sağlar
- `createSlice`: reducer + action tek yerde
- `createAsyncThunk`: async işlem (Zustand'da middleware ile yapılanın RTK karşılığı)
- **RTK Query**: TanStack Query'ye benzer data fetching — ayrı kütüphane gerektirmez
  - `createApi`, `useGetKitaplarQuery`, `useAddKitapMutation`
  - Otomatik cache, invalidate, refetch
- `configureStore`: store kurulumu
- Redux DevTools: browser extension ile state izleme
- **Ne zaman Zustand, ne zaman RTK?**
  - Küçük proje / startup / kişisel → Zustand (daha hızlı geliştirme)
  - Büyük ekip / enterprise / mevcut Redux projesi → RTK

**React/Next.js ile karşılaştırma:**

| Zustand | Redux Toolkit | Fark |
|---------|--------------|------|
| `create((set) => ({ ... }))` | `createSlice({ name, initialState, reducers })` | RTK daha verbose |
| `useKitapStore(state => state.kitaplar)` | `useSelector(state => state.kitap.kitaplar)` | Benzer selector |
| `useKitapStore.getState().ekle(id)` | `dispatch(kitapEkle(id))` | RTK action dispatch |
| TanStack Query | RTK Query | İkisi de caching yapar |

**Kod:**
```tsx
// store/kitapSlice.ts
import { createSlice, PayloadAction, createAsyncThunk } from '@reduxjs/toolkit';

// createAsyncThunk: async işlemi yönet (pending, fulfilled, rejected otomatik)
// bunu yazmasaydık: her async işlem için 3 ayrı action tipi yazardık
export const kitaplariGetir = createAsyncThunk(
  'kitap/getir',
  async (sayfa: number) => {
    const response = await api.get(`/books?page=${sayfa}`);
    return response.data as Kitap[];
  }
);

type KitapState = {
  liste: Kitap[];
  yukleniyor: boolean;
  hata: string | null;
};

const kitapSlice = createSlice({
  name: 'kitap',
  initialState: { liste: [], yukleniyor: false, hata: null } as KitapState,
  reducers: {
    // senkron action'lar buraya
    kitapSil: (state, action: PayloadAction<string>) => {
      // Immer ile direkt mutate edebilirsin — RTK built-in immer kullanır
      // bunu yazmasaydık: [...state.liste.filter(...)] yazmak zorundaydık
      state.liste = state.liste.filter(k => k.id !== action.payload);
    },
  },
  extraReducers: (builder) => {
    // async thunk durumları
    builder
      .addCase(kitaplariGetir.pending, (state) => {
        state.yukleniyor = true; // bunu yazmasaydık: loading state yoktu
      })
      .addCase(kitaplariGetir.fulfilled, (state, action) => {
        state.yukleniyor = false;
        state.liste = action.payload;
      })
      .addCase(kitaplariGetir.rejected, (state, action) => {
        state.yukleniyor = false;
        state.hata = action.error.message ?? 'Hata oluştu';
      });
  },
});

export const { kitapSil } = kitapSlice.actions;
export default kitapSlice.reducer;

// RTK Query örneği — TanStack Query'nin RTK versiyonu
import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const kitapApi = createApi({
  reducerPath: 'kitapApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Kitap'],  // cache invalidation için tag sistemi
  endpoints: (builder) => ({
    kitaplariGetir: builder.query<Kitap[], number>({
      query: (sayfa) => `/books?page=${sayfa}`,
      providesTags: ['Kitap'], // bu endpoint 'Kitap' tag'ini sağlar
    }),
    kitapEkle: builder.mutation<Kitap, Partial<Kitap>>({
      query: (kitap) => ({ url: '/books', method: 'POST', body: kitap }),
      invalidatesTags: ['Kitap'], // mutation sonrası 'Kitap' tag'li cache'i temizle
    }),
  }),
});

// Otomatik oluşan hook'lar
export const { useKitaplariGetirQuery, useKitapEkleMutation } = kitapApi;
```

**Kontrol Soruları:**
1. `createAsyncThunk` olmadan async işlemi nasıl yazardın? Neden RTK bunu kolaylaştırıyor?
2. RTK'da `immer` otomatik kullanılıyor — bu ne sağlıyor? `state.liste = []` neden spread gerektirmiyor?
3. RTK Query ile TanStack Query — hangisi hangi projede? Cache invalidation farkı ne?
4. `useSelector` her render'da çalışır — gereksiz re-render nasıl önlenir?

---

### Gün 38 — Accessibility (A11y): Ekran Okuyucu Desteği ★ YENİ

**Teorik:**
- **Neden önemli?** Büyük şirket mülakatlarında soruluyor, App Store review'da a11y kontrol ediliyor
- iOS: VoiceOver, Android: TalkBack — ekran okuyucular
- `accessibilityLabel`: ekran okuyucunun okuduğu metin — `alt` text'in karşılığı
- `accessibilityRole`: bu component ne? ('button', 'image', 'header', 'link', 'text')
- `accessibilityHint`: ek açıklama — "çift tıklayarak kitap detayına git"
- `accessibilityState`: `{ disabled, checked, selected, busy }` — component durumu
- `accessible={true}`: child'ları tek bir accessible element olarak grupla
- `accessibilityLiveRegion`: dinamik içerik değişince okuyucuya bildir ('polite', 'assertive')
- Renk kontrastı: WCAG AA standardı — `expo-navigation-bar` ile test
- Minimum dokunmatik hedef boyutu: iOS 44x44pt, Android 48x48dp

**React/Next.js ile karşılaştırma:**

| Web A11y | React Native A11y | Fark |
|---------|-------------------|------|
| `<img alt="Kitap kapağı">` | `<Image accessibilityLabel="Kitap kapağı">` | prop adı farklı |
| `<button aria-label="Sil">` | `<Pressable accessibilityLabel="Sil" accessibilityRole="button">` | role ayrı prop |
| `role="heading"` | `accessibilityRole="header"` | "heading" değil "header" |
| `aria-disabled={true}` | `accessibilityState={{ disabled: true }}` | object içinde |
| `aria-live="polite"` | `accessibilityLiveRegion="polite"` | Benzer isimler |
| `tabIndex` | `accessible={true}` / focus yönetimi | Farklı mekanizma |

**Kod:**
```tsx
// Kötü: ekran okuyucu "Buton" der, ne yaptığını bilmez
<Pressable onPress={kitapSil}>
  <Image source={silIcon} />
</Pressable>

// İyi: ekran okuyucu "Kitabı sil, çift tıklayarak sil" der
<Pressable
  onPress={kitapSil}
  accessibilityLabel="Kitabı sil"          // ne olduğunu söyle
  accessibilityRole="button"               // button olduğunu söyle
  accessibilityHint="Kitabı okuma listesinden kaldırır" // ne yapacağını söyle
  accessibilityState={{ disabled: yukleniyor }} // disabled durumu
>
  <Image
    source={silIcon}
    accessible={false} // bunu yazmasaydık: okuyucu hem image hem button okur
  />
</Pressable>

// Gruplandırma: kart içindeki her şeyi tek element gibi oku
<Pressable
  accessible={true}  // children'ı tek group olarak oku
  accessibilityLabel={`${kitap.baslik}, ${kitap.yazar} tarafından`}
  accessibilityRole="button"
>
  <Image source={{ uri: kitap.kapak }} accessible={false} />
  <Text>{kitap.baslik}</Text>   {/* ayrı ayrı okunmaz, accessible={false} gerek yok */}
  <Text>{kitap.yazar}</Text>
</Pressable>

// Dinamik içerik: yükleniyor bildirimi
<View accessibilityLiveRegion="polite">
  {yukleniyor
    ? <Text>Kitaplar yükleniyor...</Text>  // değişince ekran okuyucu okur
    : <Text>{kitaplar.length} kitap bulundu</Text>
  }
</View>
```

**Test:**
```
iOS: Ayarlar → Erişilebilirlik → VoiceOver → Aç
Android: Ayarlar → Erişilebilirlik → TalkBack → Aç

Kontrol listesi:
□ Tüm butonların accessibilityLabel'ı var mı?
□ Görsel ikonların accessibilityLabel'ı var mı?
□ Form input'larının accessibilityLabel'ı var mı?
□ Yükleniyor durumu accessibilityLiveRegion ile bildiriliyor mu?
□ Dokunmatik hedefler en az 44x44pt mi?
```

**Kontrol Soruları:**
1. `accessible={true}` ile `accessible={false}` — ne zaman hangisi kullanılır?
2. Kart component'inde hem Pressable hem içindeki Image'ın accessibilityLabel'ı olsa ne olur?
3. Renk kontrastını otomatik test eden araçlar var mı? Manuel test yeterli mi?
4. `accessibilityRole="button"` yazmadan Pressable'ı buton olarak tanımlar mı ekran okuyucu?

---

### Gün 39 — Localization: i18n ve RTL Desteği

**Teorik:**
- `i18next` + `react-i18next`: web'de kullanıyordunsa RN'de de çalışır
- `expo-localization`: cihaz dilini ve locale'ini al
- RTL (sağdan sola): Arapça/İbranice desteği — `I18nManager.isRTL`
- `I18nManager.forceRTL()`: uygulama yeniden başlatılmalı
- Pluralization, date formatting, number formatting
- Layout için RTL: `start`/`end` kullan, `left`/`right` değil

**Kontrol Soruları:**
1. `i18next` web'dekiyle aynı kurulur mu RN'de?
2. RTL layout — `start`/`end` kullanmak neden `left`/`right`'tan iyi?
3. Cihaz dili değişirse uygulama dili otomatik değişmeli mi?

---

### Gün 40 — Hafta 6 Özeti & Proje: Kitap Ekleme Formu + RTK Entegrasyonu

**Mini Proje:** Kitap ekleme formu (RHF + Zod), RTK Query ile API, NativeWind styling, accessibility label'ları ekle, Türkçe/İngilizce dil desteği.

---

# FAZ 3 — Performans, Test ve Deploy

---

## Hafta 7 — Performans Optimizasyonu

### Gün 41 — JS Thread vs UI Thread: Render Performansı

**Teorik:**
- React Native'de 3 thread: JS Thread, UI Thread (Main), Native Modules Thread
- Jank: JS thread meşgulken UI donuyor — 60fps = 16ms/frame
- `InteractionManager.runAfterInteractions()`: gezinme animasyonu bitince ağır işi yap
- `useMemo`, `useCallback`, `React.memo` — **uyarı**: her yere koymak zararlı
  - Profil et, sonra optimize et — kör memoization anti-pattern
- Hermes engine: Facebook'un JS engine'i — V8'den küçük, daha hızlı başlatma
- Profiling: React DevTools + Flipper

**React/Next.js ile karşılaştırma:**

| Web | React Native | Fark |
|-----|-------------|------|
| Main thread + Web Worker | JS Thread + UI Thread + Native Thread | 3 thread |
| `requestIdleCallback` | `InteractionManager.runAfterInteractions` | Benzer konsept |
| V8 / SpiderMonkey | Hermes (default) | Farklı engine |
| Chrome DevTools profiler | Flipper + React DevTools | Farklı araç |

**Kontrol Soruları:**
1. JS Thread meşgulken neden UI donuyor? `useNativeDriver` nasıl çözer?
2. `InteractionManager.runAfterInteractions` ne zaman kullanılır?
3. `useMemo`/`useCallback` her yere koymanın neden zararlı olabileceğini açıkla.

---

### Gün 42 — FlatList Optimizasyonu

**Teorik:**
- `getItemLayout`: tüm item'lar aynı yükseklikte → scroll to index + performans
- `initialNumToRender`, `maxToRenderPerBatch`, `windowSize`
- `removeClippedSubviews`: ekran dışındaki view'ları kaldır
- `keyExtractor`: stable key — her render'da aynı olmalı
- `React.memo` ile `renderItem`

**Kod:**
```tsx
<FlatList
  data={kitaplar}
  keyExtractor={(item) => item.id}

  // getItemLayout: bunu yazmasaydık: RN her item'ın yüksekliğini ölçer, yavaş
  getItemLayout={(_, index) => ({
    length: ITEM_HEIGHT,
    offset: ITEM_HEIGHT * index,
    index,
  })}

  initialNumToRender={10}
  maxToRenderPerBatch={5}      // her scroll batch: 5 — daha az = daha akıcı
  windowSize={10}              // ekranın 10 katı item bellekte

  renderItem={({ item }) => <MemoizedKitapKart kitap={item} />}
/>

const MemoizedKitapKart = React.memo(KitapKart, (prev, next) => {
  // true: re-render yok, false: re-render var
  // bunu yazmasaydık: listedeki herhangi bir item değişince tüm liste re-render olurdu
  return prev.kitap.id === next.kitap.id && prev.kitap.baslik === next.kitap.baslik;
});
```

**Kontrol Soruları:**
1. `getItemLayout` olmadan scroll to index neden yavaş?
2. `windowSize={5}` ile `windowSize={21}` — trade-off nedir?
3. `React.memo` karşılaştırması pahalı mı? Her zaman değer mi?

---

### Gün 43 — Image Caching ve Bundle Optimizasyonu

**Teorik:**
- `expo-image`: built-in cache — `cachePolicy` prop ('disk', 'memory', 'memory-disk')
- `FastImage` (`react-native-fast-image`): Glide/SDWebImage native cache
- Metro bundle: web'deki Webpack chunk splitting yok — tek bundle
- Lazy screen loading: Expo Router otomatik code splitting yapar
- SVG optimization: `react-native-svg-transformer`

**Kontrol Soruları:**
1. Mobil'de görsel cache neden web'den daha önemli?
2. Metro bundle splitting var mı? Web'deki chunk'lara benzer mekanizma?
3. `expo-image` cache policy seçenekleri ne zaman hangisi?

---

### Gün 44 — React Native Testing Library

**Teorik:**
- `@testing-library/react-native`: web'deki RTL ile aynı API
- `render`, `screen`, `fireEvent`, `waitFor` — hepsi aynı
- `userEvent`: daha gerçekçi kullanıcı etkileşimi
- Mock: `jest.mock('expo-router', ...)`, `jest.mock('@react-native-async-storage/async-storage')`
- Native modülleri mock'lama: `__mocks__/` klasörü
- `act()`: state güncellemelerini wrap et

**React/Next.js ile karşılaştırma:**

| Web (RTL) | React Native (RTL) | Fark |
|-----------|-------------------|------|
| `screen.getByRole('button')` | `screen.getByRole('button')` | Aynı! |
| `fireEvent.click(el)` | `fireEvent.press(el)` | press, click değil |
| `userEvent.type(input, 'abc')` | `userEvent.type(input, 'abc')` | Aynı! |

**Kod:**
```tsx
describe('KitapKart', () => {
  it('başlık ve yazarı gösterir', () => {
    render(<KitapKart baslik="Dune" yazar="Frank Herbert" kapak="" onPress={jest.fn()} />);
    expect(screen.getByText('Dune')).toBeTruthy();
    expect(screen.getByText('Frank Herbert')).toBeTruthy();
  });

  it('tıklanınca onPress çağrılır', () => {
    const onPress = jest.fn();
    render(<KitapKart baslik="Dune" yazar="FH" kapak="" onPress={onPress} />);
    fireEvent.press(screen.getByText('Dune')); // click değil press!
    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
```

**Kontrol Soruları:**
1. `fireEvent.click` neden yok?
2. `expo-router`'ı test ortamında nasıl mock'larsın?
3. Async component testi: `waitFor` ne zaman gerekli?

---

### Gün 45 — Hafta 7 Özeti & Performans Profiling

**Pratik:** Flipper kurulumu, React DevTools ile render profiling, FlatList önce/sonra karşılaştırması, Hermes aktivasyonu, accessibility audit.

---

## Hafta 8 — Deploy ve Production

### Gün 46 — EAS Build: App Store ve Play Store'a Hazırlık

**Teorik:**
- EAS (Expo Application Services): cloud build servisi
- `eas build --platform ios/android`
- `app.json` / `app.config.js`: uygulama metadata
- `eas.json`: build profilleri (development, preview, production)
- iOS: Provisioning Profile, Certificate — Apple Developer hesabı ($99/yıl)
- Android: Keystore — bir kez üret, kaybet = güncelleyemezsin
- `bundleIdentifier` (iOS) / `package` (Android): **değiştirilemez!**
- `Info.plist` ve `AndroidManifest.xml`: izin açıklamaları, deep link scheme

**React/Next.js ile karşılaştırma:**

| Web Deploy | Mobile Deploy | Fark |
|-----------|--------------|------|
| Vercel push → anlık | EAS Build → App Store review 1-3 gün | Çok daha yavaş cycle |
| HTTPS sertifikası otomatik | iOS sertifika yönetimi | Ek setup |
| Subdomain | `bundleIdentifier` — değişmez | Kritik karar! |

**Kontrol Soruları:**
1. `bundleIdentifier` neden değiştirilemez?
2. Android Keystore kaybedilirse ne olur?
3. `Info.plist`'i ne zaman elle düzenlemek zorundasın?

---

### Gün 47 — OTA Updates ve Expo Updates

**Teorik:**
- OTA: JS bundle'ı güncelle, App Store review beklemeden
- `expo-updates`: `fetchUpdateAsync()` + `reloadAsync()`
- **Kısıt:** native kod değişirse OTA çalışmaz — yeni binary gerekir
- `eas update`: yeni JS bundle'ı Expo CDN'e gönder
- Channel: production, staging, preview
- Rollback: sorunlu update'i geri al
- **CodePush** (Microsoft): Expo yerine React Native CLI projelerinde yaygın alternatif

**Kontrol Soruları:**
1. OTA neyi güncelleyebilir, neyi güncelleyemez?
2. Kullanıcı internet yokken update nasıl handle edilir?
3. Zorunlu update (force update) nasıl uygulanır?

---

### Gün 48 — Sentry ile Error Tracking

**Teorik:**
- `@sentry/react-native`: crash reporting, performance monitoring
- JavaScript hataları + native crash'ler
- `Sentry.captureException()`, `Sentry.captureMessage()`
- User context, breadcrumbs
- Source maps: minified stack trace'i orijinal koda map et — **kritik**
- Performance: transaction, span

**Kontrol Soruları:**
1. Native crash (Android/iOS) ile JS error — Sentry ikisini de yakalar mı?
2. Source map upload neden önemli?
3. Sentry vs Firebase Crashlytics — ne zaman hangisi?

---

### Gün 49 — Firebase Analytics ve Crashlytics

**Teorik:**
- `@react-native-firebase`: Firebase'in React Native SDK'sı
- Analytics: ekran izleme, custom event, funnel
- Crashlytics: native crash reports
- Remote Config: A/B test, feature flag — uygulama içinden ayar değiştir
- `@react-native-firebase` + Expo: `expo-build-properties` ile entegrasyon

**Kontrol Soruları:**
1. Expo Managed Workflow'da Firebase nasıl entegre edilir?
2. Screen tracking otomatik mi? React Navigation ile nasıl?
3. Remote Config ile feature flag: App Store review olmadan özelliği kapat/aç?

---

### Gün 50 — CI/CD: GitHub Actions ile Otomatik Build

**Teorik:**
- EAS Build webhook → GitHub Actions trigger
- `EXPO_TOKEN` secret
- PR'da preview build, main'de production build
- Test → Lint → Build pipeline

**Kod:**
```yaml
# .github/workflows/build.yml
name: EAS Build
on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: 'npm'
      - run: npm ci
      - run: npm test
      - uses: expo/expo-github-action@v8
        with:
          eas-version: latest
          token: ${{ secrets.EXPO_TOKEN }}  # bunu yazmasaydık: EAS CLI auth hatası
      - run: eas build --platform all --non-interactive  # CI'da interactive olmaz
```

**Kontrol Soruları:**
1. `--non-interactive` neden CI'da zorunlu?
2. Farklı ortamlar için farklı build profile nasıl kullanılır?
3. `eas submit` ile otomatik App Store upload — ne gereklidir?

---

### Gün 51 — Fastlane ve Maestro E2E ★ YENİ

**Teorik — Fastlane:**
- Fastlane: mobil deployment otomasyonu — şirketlerin yarısı kullanıyor
- EAS Build'dan farkı: Fastlane local/CI'da çalışır, daha fazla kontrol, Bare Workflow için ideal
- `Fastfile`: lane tanımları — her lane bir görev zinciri
- `fastlane match`: takım için sertifika yönetimi (iOS'ta çok önemli)
- `fastlane supply`: Android Play Store'a gönder
- `fastlane pilot`: iOS TestFlight'a gönder
- `fastlane gym`: iOS IPA build
- `fastlane deliver`: metadata + screenshot + binary birlikte gönder

**Teorik — Maestro E2E:**
- Maestro: React Native için en kolay E2E test aracı — Detox'tan çok daha basit
- YAML ile test yaz, cihazda çalıştır
- `maestro test flow.yaml`
- `tapOn`, `inputText`, `assertVisible`, `assertNotVisible`
- Detox'tan farkı: setup 5 dakika, Detox setup 1-2 saat; Maestro daha az flaky

**React/Next.js ile karşılaştırma:**

| Web CI/CD | Mobile (Fastlane + EAS) | Fark |
|-----------|------------------------|------|
| Vercel/Netlify otomatik | Fastlane lane ile otomatik | Daha fazla konfigürasyon |
| Playwright / Cypress E2E | Maestro E2E | Mobil gesture desteği |
| `npm test` → deploy | Test → Build → Sign → Upload | Daha çok adım |

**Kod — Fastfile:**
```ruby
# ios/fastlane/Fastfile
default_platform(:ios)

platform :ios do
  desc "TestFlight'a gönder"
  lane :beta do
    # match: ekip sertifikalarını Git repo'dan çek (şifreli)
    # bunu yazmasaydık: her developer kendi sertifikasını ayarlaması gerekir
    match(type: "appstore")

    # gym: IPA build et
    gym(scheme: "KitapApp", configuration: "Release")

    # pilot: TestFlight'a yükle
    pilot(skip_waiting_for_build_processing: true)
  end
end
```

**Kod — Maestro E2E:**
```yaml
# e2e/login_flow.yaml
appId: com.kitapapp
---
- launchApp
- tapOn: "Email"
- inputText: "test@example.com"
- tapOn: "Şifre"
- inputText: "test123"
- tapOn: "Giriş Yap"
# assertVisible: bunu yazmasaydık: giriş başarılı oldu mu bilmezdik
- assertVisible: "Anasayfa"
- assertNotVisible: "Giriş Yap"
```

**Kontrol Soruları:**
1. `fastlane match` neden gerekli? Ekip sertifika yönetimini nasıl çözer?
2. Maestro ile Detox farkı — flaky test neden Detox'ta daha yaygın?
3. E2E test'leri CI'da çalıştırmak için fiziksel cihaz gerekir mi? Alternatif?
4. `fastlane gym` ile EAS Build — aynı sonucu mu üretir? Hangisi ne zaman?

---

### Gün 52 — Hafta 8 Özeti & Final Deploy

**Pratik:** KitapApp'i TestFlight (iOS) veya Internal Test (Android) kanalına deploy et. Sentry entegrasyonu tamamla, OTA update test et, Maestro ile login flow E2E testi yaz.

---

# FAZ 4 — Final Proje: KitapApp

## Hafta 9–12 — Tüm Özelliklerle Tam Uygulama

### Proje Kapsamı

**Ekranlar:**
- Onboarding (3 slide, AsyncStorage'da "gösterildi" flag)
- Login / Register (RHF + Zod, JWT)
- Anasayfa (FlatList, infinite scroll, skeleton)
- Arama (debounced TextInput, TanStack Query veya RTK Query)
- Kitap Detay (animasyonlu header, reviews)
- Okuma Listesi (Zustand + MMKV persist, swipe to delete)
- Profil (kamera/galeri, dark mode toggle)
- Bildirimler (local + push notification)

**Teknik Gereksinimler:**
- Expo Router (file-based routing)
- TypeScript strict mode — `any` yasak
- TanStack Query veya RTK Query (server state)
- Zustand + MMKV (client state)
- NativeWind (styling)
- React Hook Form + Zod (form)
- Reanimated 3 (animasyon)
- Accessibility label'ları her component'te
- EAS Build + OTA (deploy)
- Sentry (error tracking)
- Jest + RNTL (unit/component test)
- Maestro (E2E — login flow)

---

### Gün 53 — Proje Kurulum ve Mimari Kararlar

**Kararlar:**
- Expo SDK 52+ seç
- Managed Workflow (başlangıç) — native modül gerekirse Bare'e geç
- Klasör yapısı (`app/`, `components/`, `hooks/`, `services/`, `store/`)
- Absolute imports: `@/components/...` — `tsconfig.json` paths
- API URL: `app.config.js` + EAS environment variables
- ESLint + Prettier + Husky pre-commit hook
- TypeScript strict: `"strict": true`, `"noUncheckedIndexedAccess": true`

---

### Gün 54 — Onboarding ve Auth Akışı

**Hedef:** Splash screen → onboarding (ilk kez) → login → tab navigator. Token refresh flow. Secure token storage. Tüm ekranlar erişilebilir (accessibilityLabel).

---

### Gün 55 — Anasayfa ve Kitap Listesi

**Hedef:** Kategoriler (horizontal scroll), öne çıkan kitap (büyük banner), yeni çıkanlar (FlatList infinite scroll), skeleton loading, pull to refresh.

---

### Gün 56 — Arama ve Filtreleme

**Hedef:** Debounced arama (300ms), filtre bottom sheet (kategori, yazar, yıl), sonuçlar FlatList, "sonuç yok" empty state, accessibility desteği.

---

### Gün 57 — Kitap Detay ve Okuma Listesi

**Hedef:** Parallax scroll header (Reanimated), bölüm listesi, yorumlar, "okuma listesine ekle" (Zustand), paylaş (Share API), RTK Query veya TanStack Query.

---

### Gün 58 — Profil ve Ayarlar

**Hedef:** Profil fotoğrafı (kamera/galeri), dark mode toggle, bildirim izni, i18n (TR/EN), uygulama versiyonu, logout, tüm form'larda accessibility.

---

### Gün 59 — Animasyonlar ve Polish

**Hedef:** Shared element transition (liste → detay), micro-interaction'lar (beğen butonu), haptic feedback, Maestro E2E login testi, a11y audit.

---

### Gün 60 — Test, Deploy ve Retrospektif

**Hedef:** Unit test (kritik hook'lar), RNTL component test, EAS Build production, TestFlight / Internal Test, Sentry aktivasyon, Fastlane beta lane.

**Retrospektif — React'tan React Native'e:**
- Ne beklenenden zor geldi?
- Hangi kavramlar direkt transfer oldu?
- Hangi web alışkanlıkları mobilde sorun çıkardı?

---

## GENEL KARŞILAŞTIRMA TABLOSU: React Web → React Native

| Kategori | React Web | React Native |
|----------|-----------|-------------|
| **Rendering** | Virtual DOM → gerçek DOM | Fabric → native view |
| **Styling** | CSS, Tailwind, CSS-in-JS | StyleSheet, NativeWind (sınırlı) |
| **Layout** | Flexbox + Grid + Block | Sadece Flexbox (default: column) |
| **Routing** | React Router, Next.js | Expo Router (file-based) |
| **Navigation** | URL tabanlı, history | Stack tabanlı, native gesture |
| **Storage** | localStorage, cookie | AsyncStorage, SecureStore, MMKV, Keychain |
| **Görsel** | `<img>` (boyutsuz) | `<Image>` (w/h zorunlu) |
| **Input** | `<input onChange>` | `<TextInput onChangeText>` |
| **Scroll** | CSS overflow | ScrollView (az) / FlatList (çok) |
| **Animasyon** | CSS transition, Framer Motion | Animated API, Reanimated 3 |
| **Gesture** | onMouseDown/Move/Up | react-native-gesture-handler |
| **Notification** | Web Push API (SW) | expo-notifications |
| **Camera** | MediaDevices API | expo-camera |
| **Konum** | Geolocation API | expo-location |
| **A11y** | `aria-label`, `role` | `accessibilityLabel`, `accessibilityRole` |
| **Deploy** | CDN, hosting (anlık) | App Store (review + EAS Build) |
| **Build** | Webpack/Vite/Turbopack | Metro |
| **JS Engine** | V8 (Chrome) | Hermes (default) |
| **E2E Test** | Playwright, Cypress | Maestro, Detox |
| **CI/CD** | Vercel/Netlify/GitHub Actions | EAS + Fastlane + GitHub Actions |
| **State** | Zustand/RTK (aynı) | Zustand/RTK (aynı) ✓ |
| **Form** | RHF + Zod (aynı) | `Controller` wrapper zorunlu |
| **Test** | RTL (aynı API) | RNTL (`press` vs `click`) |
| **HTTP** | fetch, Axios (aynı) | fetch, Axios (aynı) ✓ |
| **TypeScript** | Aynı | Navigation params ayrıca type edilmeli |

✓ = web'dekiyle birebir aynı çalışır

---

## ARAÇLAR VE EKOSİSTEM

| Kategori | Öneri | Alternatif |
|----------|-------|-----------|
| Framework | Expo (Managed → Bare) | React Native CLI |
| Routing | Expo Router | React Navigation |
| Styling | NativeWind | StyleSheet + tema |
| UI Kit | React Native Paper | Gluestack UI |
| State (client) | Zustand | Redux Toolkit |
| State (server) | TanStack Query | RTK Query |
| Form | React Hook Form + Zod | Formik |
| Storage | MMKV (hız) + SecureStore (token) | AsyncStorage, Keychain |
| Animasyon | Reanimated 3 | Animated API |
| Gesture | RNGH | — |
| Icons | @expo/vector-icons | react-native-vector-icons |
| Image | expo-image | react-native-fast-image |
| Test (unit) | Jest + RNTL | — |
| Test (E2E) | Maestro | Detox |
| Build | EAS Build | Fastlane |
| CI/CD | GitHub Actions + EAS | Bitrise, Codemagic |
| Error | Sentry | Firebase Crashlytics |
| Analytics | PostHog | Firebase Analytics |
| Maps | react-native-maps | — |
| Camera | expo-camera + expo-image-picker | — |

---

## SÜRÜM VE UYUMLULUK

> Expo SDK 52+ ve React Native 0.76+ (New Architecture varsayılan) kullanılır. Yeni mimari (Fabric + TurboModules + JSI) artık varsayılan — 0.82'de legacy bridge tamamen kaldırılıyor. Müfredat boyunca yeni mimari baz alınır.
