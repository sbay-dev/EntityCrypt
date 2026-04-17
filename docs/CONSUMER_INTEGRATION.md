# 🔗 دليل تكامل المشاريع المعتمدة — EntityCrypt

هذا الدليل موجّه لفِرق التطوير التي تعتمد على حزمة EntityCrypt في مشاريعها.
يوضّح كيفية استقبال الإشعارات التلقائية وتحديث التبعيات والتحقق من سلامة الحزمة.

---

## 📡 استقبال إشعارات الإصدار الجديد

عند نشر إصدار جديد من EntityCrypt، يُرسَل حدث `repository_dispatch` تلقائياً
إلى المستودعات المسجّلة. يمكنك إعداد سير عمل في مستودعك للتعامل مع هذا الحدث.

### إعداد سير عمل التحديث التلقائي

أنشئ الملف `.github/workflows/auto-update-entitycrypt.yml` في مستودعك:

```yaml
name: Auto-Update EntityCrypt

on:
  repository_dispatch:
    types: [dependency-updated]

permissions:
  contents: write
  pull-requests: write

jobs:
  update-dependency:
    # يعمل فقط عند تحديث EntityCrypt
    if: github.event.client_payload.package == 'EntityCrypt.EFCore'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Extract version
        id: version
        run: |
          VERSION="${{ github.event.client_payload.version }}"
          VERSION="${VERSION#v}"
          echo "version=$VERSION" >> $GITHUB_OUTPUT

      - name: Update EntityCrypt packages
        run: |
          find . -name "*.csproj" -exec grep -l "EntityCrypt" {} \; | while read csproj; do
            sed -i "s|Include=\"EntityCrypt\.Core\" Version=\"[^\"]*\"|Include=\"EntityCrypt.Core\" Version=\"${{ steps.version.outputs.version }}\"|g" "$csproj"
            sed -i "s|Include=\"EntityCrypt\.EFCore\" Version=\"[^\"]*\"|Include=\"EntityCrypt.EFCore\" Version=\"${{ steps.version.outputs.version }}\"|g" "$csproj"
          done

      - name: Restore and Build
        run: dotnet build --configuration Release

      - name: Create Pull Request
        uses: peter-evans/create-pull-request@v7
        with:
          commit-message: "deps: update EntityCrypt to ${{ steps.version.outputs.version }}"
          title: "⬆️ Update EntityCrypt to ${{ steps.version.outputs.version }}"
          body: |
            Automated dependency update triggered by [EntityCrypt release](https://github.com/${{ github.event.client_payload.repository }}/releases/tag/${{ github.event.client_payload.version }}).

            ### Changes
            - Updated `EntityCrypt.Core` → `${{ steps.version.outputs.version }}`
            - Updated `EntityCrypt.EFCore` → `${{ steps.version.outputs.version }}`

            > ⚠️ Review migration compatibility if this is a major/minor version bump.
          branch: deps/entitycrypt-${{ steps.version.outputs.version }}
          labels: dependencies,automated
```

### ماذا يحدث عند إصدار جديد؟

```
📦 EntityCrypt v2.1.0 تُنشر على NuGet
    │
    ▼
🔔 حدث repository_dispatch يُرسل إلى مستودعك
    │
    ▼
⚙️ سير العمل auto-update-entitycrypt ينطلق
    │
    ▼
🔄 تحديث أرقام الإصدارات في ملفات .csproj
    │
    ▼
🔨 بناء المشروع للتحقق من التوافق
    │
    ▼
📋 إنشاء Pull Request تلقائي
    │
    ▼
👀 مراجعة الفريق → دمج
```

---

## 📝 تسجيل مستودعك للإشعارات

لتلقي إشعارات تلقائية عند نشر إصدار جديد:

### 1. أرسل طلباً لمشرف EntityCrypt

أرسل طلباً يتضمن:
- اسم المستودع الكامل (مثال: `sbay-dev/MyProject`)
- افتح [Issue جديد](https://github.com/sbay-dev/EntityCrypt/issues/new) أو تواصل مع الفريق

### 2. ما يحدث من جانب EntityCrypt

يقوم المشرف بـ:

1. إضافة مستودعك إلى مصفوفة الإشعارات في `publish-nuget.yml`:
   ```yaml
   matrix:
     repo:
       - sbay-dev/WasmMvcRuntime
       - your-org/your-repo  # ← يُضاف هنا
   ```

2. تحديث صلاحيات `DISPATCH_TOKEN` ليشمل مستودعك

### 3. ما تحتاج فعله من جانبك

1. أنشئ ملف سير العمل في مستودعك (المثال أعلاه)
2. تأكد من تفعيل GitHub Actions في المستودع

---

## 🔍 التحقق من سلامة الحزمة

### التحقق عبر SHA-256

كل إصدار من EntityCrypt يتضمن ملف `SHA256SUMS.txt` في
[صفحة الإصدارات](https://github.com/sbay-dev/EntityCrypt/releases).

```powershell
# PowerShell — التحقق بعد تنزيل الحزمة
$hash = (Get-FileHash EntityCrypt.EFCore.*.nupkg -Algorithm SHA256).Hash
Write-Host "SHA-256: $hash"
# قارن مع القيمة في SHA256SUMS.txt
```

```bash
# Linux/macOS
sha256sum EntityCrypt.EFCore.*.nupkg
# قارن مع القيمة في SHA256SUMS.txt
```

### فحص الثغرات في التبعيات

```bash
# فحص شامل لكل التبعيات بما فيها المتعدية
dotnet list package --vulnerable --include-transitive

# تنسيق JSON للتحليل الآلي
dotnet list package --vulnerable --include-transitive --format json
```

### قائمة المواد البرمجية (SBOM)

ملف CycloneDX SBOM بتنسيق JSON مرفق مع كل إصدار.
يمكنك استخدامه لتدقيق سلسلة التوريد البرمجية باستخدام أدوات مثل:

```bash
# تثبيت أداة CycloneDX CLI
dotnet tool install --global CycloneDX

# تحليل SBOM
cyclonedx analyze -i EntityCrypt.EFCore-sbom.json
```

---

## 📌 إصدارات التوافق

| EntityCrypt | .NET | EF Core | ملاحظات |
|-------------|------|---------|---------|
| 2.0.x | 10.0+ | 10.0+ | الإصدار الحالي |

### سياسة الإصدارات

- **Patch** (x.y.**Z**): إصلاحات أمنية وأخطاء — تحديث آمن
- **Minor** (x.**Y**.z): ميزات جديدة — متوافق مع الإصدار السابق
- **Major** (**X**.y.z): تغييرات كبيرة — راجع ملاحظات الترحيل

---

## ⚡ التحديث اليدوي

إذا لم تكن مسجلاً في نظام الإشعارات التلقائي:

```bash
# تحديث من NuGet
dotnet add package EntityCrypt.EFCore --version X.Y.Z
dotnet add package EntityCrypt.Core --version X.Y.Z

# أو تحديث ملف .csproj يدوياً
```

```xml
<PackageReference Include="EntityCrypt.Core" Version="X.Y.Z" />
<PackageReference Include="EntityCrypt.EFCore" Version="X.Y.Z" />
```

---

## 🆘 استكشاف الأخطاء

### سير العمل لا يعمل عند إصدار جديد

1. تحقق من أن `repository_dispatch` مفعّل في إعدادات المستودع
2. تحقق من أن اسم الحدث يطابق `dependency-updated`
3. تحقق من سجلات Actions في مستودعك

### فشل البناء بعد التحديث

1. راجع [ملاحظات الإصدار](https://github.com/sbay-dev/EntityCrypt/releases)
2. تحقق من توافق إصدار .NET و EF Core
3. افتح [Issue](https://github.com/sbay-dev/EntityCrypt/issues) إذا كان الخطأ من الحزمة

### Pull Request التلقائي لا يُنشأ

1. تحقق من صلاحيات `GITHUB_TOKEN` في سير العمل:
   ```yaml
   permissions:
     contents: write
     pull-requests: write
   ```
2. تأكد من عدم وجود branch بنفس الاسم مسبقاً

---

## 📞 التواصل

- **المشاكل والاقتراحات**: [GitHub Issues](https://github.com/sbay-dev/EntityCrypt/issues)
- **الإصدارات**: [GitHub Releases](https://github.com/sbay-dev/EntityCrypt/releases)
- **حزم NuGet**: [nuget.org/packages/EntityCrypt.EFCore](https://www.nuget.org/packages/EntityCrypt.EFCore)
