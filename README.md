# Entorno de Desarrollo MongoDB y SQL Server con Docker

Este repositorio contiene una configuración optimizada de Docker Compose para ejecutar MongoDB y SQL Server en un entorno de desarrollo local, incluyendo herramientas útiles para el desarrollo y monitoreo.

## Servicios Incluidos

### MongoDB
- **MongoDB 7.0**: Servidor de base de datos
- **MongoDB Express 2.0**: Interfaz web moderna para MongoDB
- **MongoDB Exporter**: Para monitoreo con Prometheus

### SQL Server
- **SQL Server 2022**: Servidor de base de datos relacional

## Requisitos Previos

- Docker 20.10+ y Docker Compose 1.29+
- Al menos 2GB de RAM disponible
- 2 núcleos de CPU

## Configuración Inicial

1. Copia el archivo `.env.example` a `.env` y ajusta las variables según sea necesario:
   ```bash
   cp .env.example .env
   ```
   En el archivo `docker-compose.yml` deberas comentarear estas lineas para que pueda crear los contenedores: 
   ```bash
    tmpfs:
       - /var/opt/mssql/data
       - /var/opt/mssql/log
   ```

2. Edita el archivo `.env` para configurar usuarios y contraseñas seguras. Las variables más importantes son:
   ```env
   # MongoDB
   MONGO_INITDB_ROOT_USERNAME=admin
   MONGO_INITDB_ROOT_PASSWORD=admin123
   MONGO_INITDB_DATABASE=mydb
   
   # SQL Server
   MSSQL_SA_PASSWORD=TuContraseñaSegura
   MSSQL_PID=Developer
   ```

3. Asegúrate de que los puertos no estén en uso:
   - MongoDB: 27017
   - MongoDB Express: 8081
   - SQL Server: 1433
   - SSMS: 1434

## Uso Básico

### Iniciar los contenedores

```bash
docker-compose up -d
```

### Detener los contenedores

```bash
docker-compose down
```

### Acceder a MongoDB Shell

```bash
docker exec -it mongosh mongosh -u admin -p admin123 --authenticationDatabase admin
```

## Configuración de la Aplicación

La aplicación está configurada para usar tanto MongoDB como SQL Server. Las cadenas de conexión se configuran automáticamente desde el archivo `.env`.

### Estructura de Carpetas
- `src/`: Código fuente de la aplicación
  - `DDDExample.API/`: API principal
  - `DDDExample.Application/`: Lógica de negocio
  - `DDDExample.Domain/`: Entidades del dominio
  - `DDDExample.Infrastructure/`: Implementación de infraestructura (bases de datos, etc.)

### Configuración de la Base de Datos

#### MongoDB
- **Base de datos predeterminada**: `mydb`
- **Usuario administrador**: `admin`
- **Puerto**: 27017

#### SQL Server
- **Base de datos predeterminada**: `mydb`
- **Usuario**: `sa`
- **Puerto**: 1433
- **Autenticación**: SQL Server Authentication

### Acceder a las Interfaces Web

#### MongoDB Express (Web UI)
Abre tu navegador en:
- MongoDB Express 2.0: http://localhost:8081
- Mongo Express (legacy): http://localhost:8082

## Estructura de Directorios

- `.env`: Archivo de configuración de variables de entorno
- `docker-compose.yml`: Configuración de los servicios de Docker

## Puertos Expuestos

- **1433**: SQL Server
- **27017**: MongoDB
- **8081**: MongoDB Express (nueva versión)
- **8082**: Mongo Express (versión anterior)
- **9216**: MongoDB Exporter (métricas para Prometheus)

## Monitoreo

### Métricas con Prometheus

El servicio `mongodb-exporter` expone métricas en formato Prometheus en el puerto 9216.

### Estadísticas de Contenedores

```bash
docker stats mongodb mongo-express mongosh
```

## Copias de Seguridad

### Crear una copia de seguridad

```bash
docker exec mongodb sh -c 'mongodump --uri="mongodb://$MONGO_INITDB_ROOT_USERNAME:$MONGO_INITDB_ROOT_PASSWORD@localhost:27017" --archive' > backup_$(date +%Y%m%d_%H%M%S).archive
```

### Restaurar desde una copia de seguridad

```bash
docker exec -i mongodb sh -c 'mongorestore --uri="mongodb://$MONGO_INITDB_ROOT_USERNAME:$MONGO_INITDB_ROOT_PASSWORD@localhost:27017" --archive' < backup_file.archive
```
## Personalización

### Agregar más bases de datos o usuarios

1. Crea un nuevo script en el directorio `init-mongo/` con extensión `.js`
2. Los scripts se ejecutarán en orden alfabético

### Configuración de Recursos

Puedes ajustar los límites de recursos (CPU/memoria) en el archivo `docker-compose.yml` en la sección `deploy.resources` de cada servicio.

## Solución de Problemas

### Ver logs de los contenedores

```bash
docker-compose logs -f
```

### Ver logs de MongoDB

```bash
docker logs -f mongodb
```

### Reiniciar un servicio

```bash
docker-compose restart mongodb
```

## Seguridad

⚠️ **Importante**: Esta configuración es para desarrollo local. Para entornos de producción:

1. Cambia todas las contraseñas predeterminadas
2. No expongas los puertos de administración a Internet
3. Configura autenticación y autorización adecuadas
4. Considera usar redes privadas y VPN

## Licencia

Este proyecto está bajo la Licencia MIT.
