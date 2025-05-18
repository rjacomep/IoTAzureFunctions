## 🔧 Configuración de Azure Pipelines con Azure Function App usando UAMI

Este proyecto implementa una integración segura y automatizada entre **GitHub**, **Azure DevOps Pipelines** y **Azure Function App**, utilizando una **Identidad Administrada por el Usuario (UAMI)**. Esta configuración evita el uso de secretos y no requiere permisos de administrador en Azure Active Directory (AAD), solo permisos sobre la suscripción.

---

### 🛠️ Pasos de configuración

#### 1. Crear una UAMI (User Assigned Managed Identity)

```bash
az identity create --name uami-iothubandroid --resource-group iothost --location centralus
