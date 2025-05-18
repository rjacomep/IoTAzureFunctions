## 🔧 Configuración de Azure Pipelines con Azure Function App usando UAMI

Este proyecto implementa una integración segura y automatizada entre **GitHub**, **Azure DevOps Pipelines** y **Azure Function App**, utilizando una **Identidad Administrada por el Usuario (UAMI)**. Esta configuración evita el uso de secretos y no requiere permisos de administrador en Azure Active Directory (AAD), solo permisos sobre la suscripción.

---

### 🛠️ Pasos de configuración

#### 1. Crear una UAMI (User Assigned Managed Identity)

Este comando crea una identidad administrada por el usuario (UAMI) en el grupo de recursos y región especificados. Esta identidad será utilizada para la autenticación segura en Azure DevOps o GitHub Actions.

```bash
az identity create --name uami-iothubandroid --resource-group iothost --location centralus
```

**Salida esperada:**
```json
{
  "clientId": "22a86721-c711-4b57-ba51-2e5470be6768",
  "id": "/subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42/resourcegroups/iothost/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-iothubandroid",
  "location": "centralus",
  "name": "uami-iothubandroid",
  "principalId": "da304448-59a9-42b9-b1a1-4c3ea3db9446",
  "resourceGroup": "iothost",
  "systemData": null,
  "tags": {},
  "tenantId": "fd766edd-8bea-4c99-8672-56d1cabc2706",
  "type": "Microsoft.ManagedIdentity/userAssignedIdentities"
}
```
- **¿Qué hace?**  
  Crea una identidad administrada reutilizable para autenticación segura en recursos de Azure.

---

#### 2. Asignar la UAMI a la Azure Function App

Este comando asocia la identidad creada en el paso anterior a tu Azure Function App, permitiendo que la función use esa identidad para autenticarse contra otros recursos de Azure.

```bash
az functionapp identity assign --name iothubandroid --resource-group iothost --identities /subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42/resourceGroups/iothost/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-iothubandroid
```

**Salida esperada:**
```json
{
  "principalId": "e3bd61de-1fd9-4aa3-a71b-d89aa112f854",
  "tenantId": "fd766edd-8bea-4c99-8672-56d1cabc2706",
  "type": "SystemAssigned, UserAssigned",
  "userAssignedIdentities": {
    "/subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42/resourcegroups/iothost/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-iothubandroid": {
      "clientId": "22a86721-c711-4b57-ba51-2e5470be6768",
      "principalId": "da304448-59a9-42b9-b1a1-4c3ea3db9446"
    }
  }
}
```
- **¿Qué hace?**  
  Asocia la UAMI a la Function App para que pueda usarla como identidad al acceder a otros recursos de Azure.

---

#### 3. Asignar permisos a la UAMI en la suscripción

Este comando otorga el rol de "Contributor" a la UAMI sobre el alcance de la suscripción, permitiéndole realizar operaciones necesarias sobre los recursos.

```bash
az role assignment create --assignee da304448-59a9-42b9-b1a1-4c3ea3db9446 --role Contributor --scope /subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42
```

**Salida esperada:**
```json
{
  "condition": null,
  "conditionVersion": null,
  "createdBy": null,
  "createdOn": "2025-05-17T22:55:51.177367+00:00",
  "delegatedManagedIdentityResourceId": null,
  "description": null,
  "id": "/subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42/providers/Microsoft.Authorization/roleAssignments/3b8e6a3e-6f40-4e7c-a371-01118c038a2d",
  "name": "3b8e6a3e-6f40-4e7c-a371-01118c038a2d",
  "principalId": "da304448-59a9-42b9-b1a1-4c3ea3db9446",
  "principalType": "ServicePrincipal",
  "roleDefinitionId": "/subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c",
  "scope": "/subscriptions/ff9f0c6d-78a0-4af8-9fb1-ca98e7190b42",
  "type": "Microsoft.Authorization/roleAssignments",
  "updatedBy": "b8c48c71-2082-4287-a9e1-99adb438ec89",
  "updatedOn": "2025-05-17T22:55:51.363372+00:00"
}
```
- **¿Qué hace?**  
  Da permisos de "Contributor" a la UAMI para que pueda gestionar recursos dentro de la suscripción.

---

#### 4. Crear una credencial federada para la UAMI

Este comando crea una credencial federada en la UAMI, permitiendo que servicios externos (como Azure DevOps o GitHub Actions) puedan autenticarse usando tokens OIDC y actuar como la UAMI, sin necesidad de secretos.

```powershell
New-AzFederatedIdentityCredential -ResourceGroupName iothost -IdentityName uami-iothubandroid -Name AzureConnection-UAMI -Issuer "https://vstoken.dev.azure.com/d9f74a03-4b85-485a-aeb8-7048b71a672d" -Subject "sc://rjacomepiot/iothost/AzureConnection-UAMI" -Audience "api://AzureADTokenExchange"
```

**Salida obtenida:**
```text
WARNING: Upcoming breaking changes in the cmdlet 'New-AzFederatedIdentityCredential' :
...
Name                 Issuer                                                             Subject                                       Audience
----                 ------                                                             -------                                       --------
AzureConnection-UAMI https://vstoken.dev.azure.com/d9f74a03-4b85-485a-aeb8-7048b71a672d sc://rjacomepiot/iothost/AzureConnection-UAMI {api://AzureADTokenExchange}
```
- **¿Qué hace?**  
  Permite que Azure Pipelines (u otro servicio compatible con OIDC) use la UAMI para autenticarse en Azure, sin secretos.  
  - **Issuer:** URL del emisor de tokens OIDC (por ejemplo, Azure DevOps o GitHub).
  - **Subject:** Identificador único del pipeline o conexión.
  - **Audience:** Valor esperado por Azure para aceptar el token.

---

#### 5. Verificar las credenciales federadas configuradas

Puedes listar todas las credenciales federadas asociadas a la UAMI con:

```powershell
Get-AzFederatedIdentityCredential -ResourceGroupName iothost -IdentityName uami-iothubandroid
```

**Salida obtenida:**
```text
Name                 Issuer                                                             Subject                                                     Audience
----                 ------                                                             -------                                                     --------
devops-federation    https://pipelines.azure.com                                        system:serviceaccount:iothost:AzureConnection-AgentAssigned {api://AzureADTokenExchange}
AzureConnection-UAMI https://vstoken.dev.azure.com/d9f74a03-4b85-485a-aeb8-7048b71a672d sc://rjacomepiot/iothost/AzureConnection-UAMI               {api://AzureADTokenExchange}
```
- **¿Qué hace?**  
  Muestra todas las credenciales federadas configuradas para la UAMI, útiles para verificar que la integración está lista.

---

> **Resumen:**  
> 1. Se crea una identidad administrada (UAMI).  
> 2. Se asocia la UAMI a la Function App.  
> 3. Se otorgan permisos a la UAMI sobre la suscripción.  
> 4. Se crea una credencial federada para permitir autenticación OIDC desde Azure DevOps/GitHub Actions.  
> 5. Se verifica la configuración.  
> Así, tu pipeline podrá autenticarse y desplegar sin secretos ni contraseñas.
