# Запуск и сдача лаб 5–6

Корень: `C:\Users\Ardor\Desktop\c_labs`.
Нужны Docker Desktop (Linux Engine), .NET SDK 8 и для копии кольца Python 3.
В этой сессии SDK установлен отдельно, PATH не изменялся:
`C:\Users\Ardor\AppData\Local\Temp\cline-dotnet8\sdk-clean\dotnet.exe`.
В дальнейшем можно использовать обычный `dotnet` после установки SDK.

## Уже подготовленный стенд

Primary/Replica сохранены; на трёх дополнительных экземплярах созданы:
- tarot_shard — modulo, 100000 readings и 100000 reading_cards;
- tarot_ring — точная копия того же набора с размещением consistent.
БД шардов: localhost:5441/5442/5443, пользователь/пароль tarot/tarot.
Имена БД различаются! Primary:5437, БД/user/password test_project_db.
Тестовый API после проверки остановлен; данные и контейнеры оставлены.

```powershell
$dotnet = 'C:\Users\Ardor\AppData\Local\Temp\cline-dotnet8\sdk-clean\dotnet.exe'
# Запуск modulo (отдельное окно терминала; Ctrl+C для остановки)
& 'C:\Users\Ardor\Desktop\c_labs\lab5\run-service.ps1' -Dotnet $dotnet
# После остановки modulo — запуск кольца на той же HTTP-точке:
& 'C:\Users\Ardor\Desktop\c_labs\lab5\run-service.ps1' -Dotnet $dotnet -Strategy consistent
```

Swagger: http://localhost:5085 . Примеры:
- GET /api/reading?userId=1&pageSize=3
- GET /api/reading?pageSize=20
- GET /api/stats/readings-by-status
- GET /api/stats/deck-usage
- GET /api/stats/spread-stats

## Подготовка с нуля (не повторять setup на заполненных БД)

1. Запустить исходный Compose и применить существующие Liquibase-миграции по инструкции проекта.
2. Запустить три дополнительных PostgreSQL:
```powershell
docker compose -f 'C:\Users\Ardor\Desktop\c_labs\lab5\docker-compose.shards.yml' up -d
& 'C:\Users\Ardor\Desktop\c_labs\lab5\run-service.ps1' -Dotnet $dotnet -Action setup
python -B 'C:\Users\Ardor\Desktop\c_labs\lab5\copy-consistent.py'
```
Setup генерирует настоящие сущности сервиса для существующих пользователей и справочников;
не меняет readings в Primary. Добавляет Primary sequence sharding_reading_ids и таблицы шардов.
Запускать только при остановленных API/писателях, не параллельно: это учебная инициализация,
не online-миграция. При частичном сбое использовать новые пустые БД/volumes, не удалять данные вслепую.
copy-consistent.py рассчитан на проверенный двухпользовательский seed, отказывается перезаписывать
существующие tarot_ring и сверяет все колонки. Он не универсальный migrator.

## Тесты

Остановить API перед сборкой: Windows блокирует исполняемый файл работающего приложения.
```powershell
& $dotnet build 'C:\Users\Ardor\Desktop\c_labs\test_project.csproj'
& $dotnet test 'C:\Users\Ardor\Desktop\c_labs\test_project.csproj' --no-build
```
Полный набор включает реальный интеграционный тест: нужны подготовленные PostgreSQL,
Primary и пользователь 1. Без внешних БД старые/unit тесты:
```powershell
& $dotnet test 'C:\Users\Ardor\Desktop\c_labs\test_project.csproj' --no-build --filter 'Category!=Integration'
```
Для проверки кольца используйте run-service.ps1 -Action test -Strategy consistent.
Параметры окружения скрипт восстанавливает после завершения.

## pgAdmin и защита

Открыть REPORT.md и контрольные_вопросы.md в каталогах lab5/lab6.
SQL: `C:\Users\Ardor\Desktop\c_labs\lab5\pgadmin_lab5.sql` и
`C:\Users\Ardor\Desktop\c_labs\lab6\pgadmin_lab6.sql`.
Запускать в каждой БД шарда. SQL не удаляет данные; функции лабы 5 только session-local.
Сравнение 3→4 — расчёт необходимого переноса, не переключение живого кластера.
Изменение fingerprint без копирования данных недопустимо.
Python experiment_output.md — дополнительная синтетическая модель, не измерения текущего сервиса.

Для отказа остановить только lab5_shard2, проверить 503 и восстановить контейнер.
Дождаться pg_isready перед повторным запросом. Не выполнять down -v: это удалит данные.
Лабы 1–4 запускаются по старой инструкции без переменных Sharding__Enabled.
