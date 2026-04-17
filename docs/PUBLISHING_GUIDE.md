# 📦 دليل النشر — EntityCrypt

هذا الدليل يوثّق عملية نشر حزم EntityCrypt على NuGet والبنية التحتية لـ CI/CD.

---

## 🏗️ بنية CI/CD

| سير العمل | المحفّز | الوظيفة |
|-----------|---------|---------|
| [`ci.yml`](../.github/workflows/ci.yml) | Push إلى `main` + Pull Requests | بناء، اختبار، تحليل جودة، فحص ثغرات |
| [`security-audit.yml`](../.github/workflows/security-audit.yml) | أسبوعي (الاثنين 06:00 UTC) + يدوي | فحص ثغرات + إنشاء SBOM + فتح issue تلقائي |
| [`publish-nuget.yml`](../.github/workflows/publish-nuget.yml) | وسم `v*` جديد | بوابة أمنية → بناء واختبار ونشر → إشعار المشاريع المعتمدة |

---

## 🔄 خطوات النشر

### 1. تحديث الإصدار

حدّث رقم الإصدار في كلا المشروعين:

```xml
<!-- src/EntityCrypt.Core/EntityCrypt.Core.csproj -->
<Version>X.Y.Z</Version>

<!-- src/EntityCrypt.EFCore/EntityCrypt.EFCore.csproj -->
<Version>X.Y.Z</Version>
```

### 2. تشغيل الاختبارات محلياً

```bash
dotnet test src/EntityCrypt.EFCore/EntityCrypt.EFCore.csproj
```

### 3. Commit والتوسيم

```bash
git add -A
git commit -m "release: vX.Y.Z - وصف التغييرات"
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin main --tags
```

### 4. التنفيذ التلقائي

عند دفع الوسم `vX.Y.Z`، يتم تلقائياً:

```
┌─────────────────┐     ┌──────────────────────┐     ┌────────────────────┐
│  Security Gate   │────▶│  Build Test Publish   │────▶│  Notify Dependents │
│                  │     │                      │     │                    │
│  • فحص ثغرات    │     │  • بناء Release       │     │  • إرسال حدث       │
│  • حظر High/    │     │  • تشغيل اختبارات     │     │    repository_     │
│    Critical      │     │  • إنشاء .nupkg      │     │    dispatch        │
│                  │     │  • حساب SHA-256       │     │  • لكل مستودع      │
│                  │     │  • إنشاء SBOM         │     │    معتمد           │
│                  │     │  • نشر على NuGet      │     │                    │
│                  │     │  • إنشاء GitHub       │     │                    │
│                  │     │    Release            │     │                    │
└─────────────────┘     └──────────────────────┘     └────────────────────┘
```

---

## 🔑 المفاتيح والأسرار المطلوبة

| السر | النوع | الغرض |
|------|-------|-------|
| `NUGET_API_KEY` | NuGet API Key | نشر الحزم على nuget.org |
| `DISPATCH_TOKEN` | GitHub Fine-grained PAT | إرسال أحداث `repository_dispatch` للمستودعات المعتمدة |

### إعداد `NUGET_API_KEY`

1. انتقل إلى [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
2. أنشئ مفتاح جديد:
   - **Glob Pattern**: `EntityCrypt.*`
   - **Scopes**: Push new packages and package versions, Unlist
   - **Expiry**: سنة واحدة
3. أضف المفتاح كسر في المستودع:
   ```bash
   gh secret set NUGET_API_KEY --repo sbay-dev/EntityCrypt
   ```

### إعداد `DISPATCH_TOKEN`

1. انتقل إلى [github.com/settings/tokens](https://github.com/settings/tokens?type=beta) (Fine-grained PAT)
2. أنشئ رمز وصول جديد:
   - **Repository access**: حدد المستودعات المعتمدة (مثلاً `sbay-dev/WasmMvcRuntime`)
   - **Permissions**:
     - Contents: Read and Write
     - Metadata: Read-only
3. أضف الرمز كسر في المستودع:
   ```bash
   gh secret set DISPATCH_TOKEN --repo sbay-dev/EntityCrypt
   ```

> ⚠️ **تنبيه**: `DISPATCH_TOKEN` هو رمز GitHub PAT وليس مفتاح NuGet. يُستخدم لإرسال إشعارات إلى المستودعات المعتمدة.

---

## 📋 الحزم المنشورة

| الحزمة | الوصف |
|--------|-------|
| [`EntityCrypt.Core`](https://www.nuget.org/packages/EntityCrypt.Core) | مكتبة التشفير الأساسية (AES-256, ML-KEM-768, Merkle) |
| [`EntityCrypt.EFCore`](https://www.nuget.org/packages/EntityCrypt.EFCore) | تكامل EF Core الشفاف |

كلتا الحزمتين تُنشر بنفس رقم الإصدار في كل عملية نشر.

---

## 🔔 نظام الإشعارات (Repository Dispatch)

عند نشر إصدار جديد بنجاح، يتم إرسال حدث `dependency-updated` تلقائياً إلى المستودعات المعتمدة.

### المستودعات المُبلَّغة حالياً

| المستودع | الحالة |
|----------|--------|
| `sbay-dev/WasmMvcRuntime` | ✅ مفعّل |

### إضافة مستودع جديد للإشعارات

1. أضف المستودع إلى مصفوفة `matrix.repo` في [`publish-nuget.yml`](../.github/workflows/publish-nuget.yml):

```yaml
notify-dependents:
  strategy:
    matrix:
      repo:
        - sbay-dev/WasmMvcRuntime
        - sbay-dev/NewProject          # ← أضف هنا
```

2. تأكد من أن `DISPATCH_TOKEN` لديه صلاحية الوصول إلى المستودع الجديد:
   - عدّل رمز PAT في إعدادات GitHub
   - أضف المستودع الجديد إلى قائمة المستودعات المصرح بها

### بنية الحدث المُرسَل

```json
{
  "event_type": "dependency-updated",
  "client_payload": {
    "package": "EntityCrypt.EFCore",
    "version": "v2.0.2",
    "repository": "sbay-dev/EntityCrypt"
  }
}
```

---

## 🔒 ضمانات الأمان

### بوابة أمنية قبل النشر

- فحص الثغرات لكل مشروع
- حظر تلقائي عند وجود ثغرات **High** أو **Critical**
- تقرير مفصّل في GitHub Step Summary

### التوقيع والتحقق

| العنصر | التفاصيل |
|--------|----------|
| SHA-256 Checksums | ملف `SHA256SUMS.txt` مرفق مع كل إصدار |
| CycloneDX SBOM | قائمة مواد برمجية بتنسيق JSON مرفقة مع كل إصدار |
| GitHub Release | إصدار تلقائي مع ملاحظات الإصدار المُولّدة |

### تقارير محفوظة

تُحفظ التقارير كـ GitHub Artifacts لمدة **90 يوم**:
- نتائج الاختبارات
- ملفات SHA-256
- ملفات SBOM

---

## 🔄 إعادة النشر

إذا فشل النشر (بسبب صلاحيات مثلاً)، يمكنك إعادة تشغيل سير العمل من واجهة GitHub Actions دون إعادة إنشاء الوسم.

```bash
# أو من سطر الأوامر
gh run rerun <run_id> --repo sbay-dev/EntityCrypt
```

---

## 📅 الفحص الأمني الدوري

يعمل فحص أمني أسبوعي تلقائياً كل يوم اثنين الساعة 06:00 UTC:

- فحص ثغرات لكل التبعيات
- إنشاء SBOM محدّث
- فتح GitHub Issue تلقائي عند اكتشاف ثغرات

يمكنك أيضاً تشغيله يدوياً من صفحة Actions.
