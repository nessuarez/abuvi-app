using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <summary>
    /// Loads the association's camp history: 31 venues and 50 completed
    /// editions covering 1976-2025, the backbone of the 50th anniversary section.
    ///
    /// Why a migration and not the Abuvi.Setup importer: SafetyGuard.EnsureImportAllowedAsync
    /// refuses to import into a production database whose camps table already has rows, and it
    /// does. A migration applies itself on deploy, since Program.cs calls MigrateAsync at startup.
    ///
    /// The identifiers come from docs/CAMPAMENTOS_HISTORICOS.csv and
    /// docs/CAMPAMENTOS_EDICIONES_HISTORICOS.csv, so every environment ends up with the same ones.
    /// Note the development importer discards the CSV id and generates its own, so a database
    /// seeded by hand will not match this one — reload it from here if the two need to agree.
    ///
    /// Written with raw SQL rather than InsertData or HasData: it needs the conflict handling
    /// below, and HasData would drag these 81 identifiers through every
    /// future model snapshot.
    /// </summary>
    public partial class SeedHistoricalCamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Venues are skipped when one of the same name already exists, so re-running this,
            // or running it against a database that already holds a venue as a prospecting
            // candidate, cannot produce two rows for the same physical place.
            migrationBuilder.Sql(@"
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'fca8644d-33a7-5fb7-a74a-9e3a426ef8fc'::uuid, 'Arcas del Villar', 'Cuenca', 'Cuenca', 39.9886745, -2.1140398, 'ChIJcTQ4qU9gXQ0RdinFThgKPWU', '16123 Arcas, Cuenca, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Arcas del Villar'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '2d01acba-c387-50af-915b-38a05d0d4712'::uuid, 'Boñar', 'León', 'León', 42.8661879, -5.3235259, 'ChIJOdLQnGXGNw0REpnnF-xaRWA', 'Boñar, León, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Boñar'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '3f25df97-5fad-5e0d-8baf-72773ad40632'::uuid, 'Cabañeros', 'Ciudad Real', 'Ciudad Real', 38.9760631, -3.9141793, 'Ej5Bdi4gUGFycXVlIGRlIENhYmHDsWVyb3MsIDEzMDA1IENpdWRhZCBSZWFsLCBDZGFkLiBSZWFsLCBTcGFpbiIuKiwKFAoSCQm_SXkww2sNEcN5PgmVmlakEhQKEglNF5c9s9xrDRErnOex6COv7w', 'Av. Parque de Cabañeros, 13005 Ciudad Real, Cdad. Real, España', 'route', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Cabañeros'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '56b12806-efc9-56fd-ba6e-ffa2a5e18dcc'::uuid, 'Casillas de Ranera', 'Cuenca', 'Cuenca', 39.7795614, -1.2716524, 'ChIJu6v1z8dNZw0Ru1cTItpGw3I', '16321 Casillas de Ranera, Cuenca, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Casillas de Ranera'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '0e940eea-3792-53d2-a944-b41f39f81f43'::uuid, 'Cervera de Pisuerga', 'Palencia', 'Palencia', 42.8668915, -4.4992928, 'ChIJZ2ru7uxbSA0RLGZWoV9H9yg', '34840 Cervera de Pisuerga, Palencia, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Cervera de Pisuerga'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'f7792268-54ac-5065-a1e4-79cf321eeeff'::uuid, 'Condado de Castilnovo', 'Segovia', 'Segovia', 41.2451106, -3.7485874, 'ChIJTR1XsB8ARA0RS7CPth4fCbw', 'Condado de Castilnovo, 40318, Segovia, España', 'administrative_area_level_4 political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Condado de Castilnovo'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '8c9f723d-df6d-5769-aeac-12a198a352b3'::uuid, 'Condemios de Arriba', 'Guadalajara', 'Guadalajara', 41.215671, -3.1255912, 'ChIJk4MX40KAQw0RzJ7nU-BYJd0', '19275 Condemios de Arriba, Guadalajara, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Condemios de Arriba'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '936e008f-1193-5ece-bd42-422119c019f4'::uuid, 'Covaleda', 'Soria', 'Soria', 41.9354883, -2.8831158, 'ChIJ3w3RzUUcRQ0RjnhIFpKoqu0', '42157 Covaleda, Soria, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Covaleda'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'c5ed2f53-406f-501b-a22c-a450211c099c'::uuid, 'El Bosque', 'Cádiz', 'Cádiz', 36.7580042, -5.5061788, 'ChIJPZ8WYcYTDQ0RJX7NAnbaHm8', '11670 El Bosque, Cádiz, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('El Bosque'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '2e24b0b2-d59c-5bde-a556-53a40af78064'::uuid, 'Espinosa de los Monteros', 'Burgos', 'Burgos', 43.0773479, -3.5521721, 'ChIJySyunFImTw0RcIXg1Z9CMqs', '09560 Espinosa de los Monteros, Burgos, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Espinosa de los Monteros'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'aa98df4b-bdfc-5d0e-8bce-8427d204bcf1'::uuid, 'Jerte', 'Cáceres', 'Cáceres', 40.2224705, -5.7519844, 'ChIJv205BBZLPg0Rf2Cjr6Xasdk', '10612 Jerte, Cáceres, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Jerte'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '1abbb90d-fed7-5b74-b218-a2efdd1fd337'::uuid, 'Los Palancares', 'Cuenca', 'Cuenca', 40.0586715, -2.1353947, 'EiZDLiBsb3MgUGFsYW5jYXJlcywgMTYwMDMgQ3VlbmNhLCBTcGFpbiIuKiwKFAoSCbHx0FBbZ10NES-KZNpkSShEEhQKEgnDUZc2QWddDRGS0ozQbquBbg', 'C. los Palancares, 16003 Cuenca, España', 'route', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Los Palancares'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'bccef80e-6da8-5faa-a3e6-cf9757fb9b0d'::uuid, 'Matapozuelos', 'Valladolid', 'Valladolid', 41.4130125, -4.7911681, 'ChIJo1weK7ZGRw0RFG3C-hapvo0', '47230 Matapozuelos, Valladolid, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Matapozuelos'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '1819f627-0817-5be3-b060-9e1bca742115'::uuid, 'Molino de Butrera', 'Burgos', 'Burgos', 42.9990081, -3.5759606, 'ChIJ_akjiYEvTw0R1aMNIpFvl24', 'C. Butrera, s/n, 09568 Butrera, Burgos, España', 'establishment point_of_interest', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Molino de Butrera'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'da9a1589-4267-5061-bae0-8df95de05eb4'::uuid, 'Montes Universales', 'Teruel', 'Teruel', 40.375, -1.739722, 'ChIJP0mhffajXQ0R3MVEDw1dUKY', 'Montes Universales, 44115, Teruel, España', 'establishment natural_feature', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Montes Universales'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '580f6bdd-4cd3-5651-8972-3c02bdd9bb07'::uuid, 'Montes de Talayuelas', 'Cuenca', 'Cuenca', 39.8386504, -1.2979352, 'ChIJu-wVxo9RZw0ReaR-RZfoe-E', 'Cam. Tejeda, 16320 Talayuelas, Cuenca, España', 'establishment point_of_interest', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Montes de Talayuelas'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '6ffc306b-8898-5b27-a7b8-9030558097e5'::uuid, 'Mora de Rubielos', 'Teruel', 'Teruel', 40.2510415, -0.754289, 'ChIJvW54AGI3Xg0Ryd7ZFs3n6wo', '44400 Mora de Rubielos, Teruel, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Mora de Rubielos'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'a030d43d-7ccd-5622-bbe8-7dee4f372cb9'::uuid, 'Oto', 'Huesca', 'Huesca', 42.5983622, -0.128148, 'ChIJF4PScrf5Vw0R_5BGQcMHgNQ', '22370 Oto, Huesca, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Oto'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'cc08adc4-ac6c-521b-bccd-f70060367bd1'::uuid, 'Palacio de la Teyeria, Mestas de Con', 'Asturias', 'Asturias', 43.340613, -5.018647, 'ChIJN-XM9vzeSQ0R5YRnnnqIJe0', 'Carretera de Llano s/n, 33556 Mestas de Con, Asturias, España', 'establishment lodging point_of_interest', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Palacio de la Teyeria, Mestas de Con'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '4ab51f0f-3eed-50e4-b096-3f65098b89a3'::uuid, 'Pola de Gordon', 'León', 'León', 42.8543772, -5.6714331, 'ChIJezfFX0GmNw0RGF-79QjLwao', '24600 La Pola de Gordón, León, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Pola de Gordon'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'f0d01b56-1673-5439-9eca-790440e72be6'::uuid, 'Quintanar de la Sierra', 'Burgos', 'Burgos', 41.9849486, -3.0363399, 'ChIJofJMs5QGRQ0RGDIr-ADxp64', '09670 Quintanar de la Sierra, Burgos, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Quintanar de la Sierra'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '9ddaa033-b8b1-5986-85cb-9bb53b47cf7c'::uuid, 'San Juan de Riópar', 'Albacete', 'Albacete', 38.4797927, -2.447208, 'ChIJ6UQaKDj5ZQ0RNdeOHaEN2EM', 'Carretera, CM-3204, km 5, 02450 Riópar, Albacete, España', 'establishment point_of_interest', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('San Juan de Riópar'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '406b0edb-366f-54d7-8f71-1f856371c3c4'::uuid, 'San Martín del Castañar', 'Salamanca', 'Salamanca', 40.5218032, -6.0642792, 'ChIJ3z60i9P0Pg0R1wUkkWcXcgA', '37659 San Martín del Castañar, Salamanca, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('San Martín del Castañar'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '185e3b5e-4a4f-5f06-b67c-23d04e69fb4e'::uuid, 'San Pedro de las Herrerías', 'Zamora', 'Zamora', 41.9033059, -6.3807881, 'ChIJCxHHLHSIOQ0RYnuEOUeRmpg', '49560 San Pedro de las Herrerías, Zamora, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('San Pedro de las Herrerías'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '8449e2be-827e-5a52-a98b-8ed69a5f13db'::uuid, 'Selva de Oza', 'Huesca', 'Huesca', 42.7024393, -0.7519191, 'ChIJjR8rHqt6Vw0RBU8uh7zEZgo', 'Selva de Oza, 22720, Huesca, España', 'establishment natural_feature', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Selva de Oza'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'fea1dfaf-c442-5803-898e-8447d7351b6f'::uuid, 'Sierra de San Vicente', 'Toledo', 'Toledo', 40.0848454, -4.8520636, 'ChIJNTtb55sRQA0RnfKPO3xJj24', 'Sierra de San Vicente, Toledo, España', 'administrative_area_level_3 political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Sierra de San Vicente'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '6b38253e-4bd9-554b-98c9-709bfcad63d6'::uuid, 'Sierra de Segura', 'Jaén', 'Jaén', 37.9766667, -2.7761111, 'ChIJkTAejjJ1bw0RskfaPNWEMMo', 'Sierra de Segura, 23290, Jaén, España', 'establishment natural_feature', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Sierra de Segura'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '4b209c84-2bda-503f-b053-fa3eaa90bad8'::uuid, 'Sierra del Moncayo', 'Zaragoza', 'Zaragoza', 41.7675, -1.8216667, 'ChIJZ-ZFNj2oWw0RhKypjmBbPXQ', 'Moncayo, 50590, Zaragoza, España', 'establishment natural_feature', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Sierra del Moncayo'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT '39f5ff0c-e652-57d5-bfac-8ee52d0039b4'::uuid, 'Ulzama', 'Navarra', 'Navarra', 43.0001899, -1.6727153, 'ChIJIYgyaAHwUA0REPrOCGWMAQQ', 'Ultzama, Navarra, España', 'administrative_area_level_4 political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Ulzama'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'e13bca3a-3a05-53a1-b74d-491adeb3cf40'::uuid, 'Valle de Pineta', 'Huesca', 'Huesca', 42.6753784, 0.0879363, 'ChIJ9Sbljo4EqBIRlvoMy66sVf8', 'Valle de Pineta, 22351, Huesca, España', 'establishment natural_feature', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Valle de Pineta'))
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camps (id, name, location, province, latitude, longitude, google_place_id, formatted_address, place_types, price_per_adult, price_per_child, price_per_baby, is_active, created_at, updated_at)
            SELECT 'cf195596-1570-558f-97f9-1b66f7665b2a'::uuid, 'Villamanín', 'León', 'León', 42.9389054, -5.6556545, 'ChIJe8Y4GkipNw0RCNmuOWltUhU', '24680 Villamanín, León, España', 'locality political', 0, 0, 0, TRUE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            WHERE NOT EXISTS (SELECT 1 FROM camps WHERE LOWER(name) = LOWER('Villamanín'))
            ON CONFLICT (id) DO NOTHING;
            ");

            // Editions resolve their venue by name rather than trusting the CSV campId, so an
            // edition still lands on the pre-existing venue when the insert above was skipped.
            // The (camp, year) guard mirrors the duplicate check in CampEditionImporter.
            migrationBuilder.Sql(@"
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'ebaaf7b7-2c82-4fa1-9b9a-2fd2996a1194'::uuid, c.id, 1976, TIMESTAMPTZ '1976-08-15 00:00:00+00', TIMESTAMPTZ '1976-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Valle de Pineta')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1976)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'ded1aa90-e871-4e4d-bc0b-fbce18e3cd05'::uuid, c.id, 1977, TIMESTAMPTZ '1977-08-15 00:00:00+00', TIMESTAMPTZ '1977-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Selva de Oza')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1977)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '1b587207-438d-4156-bc14-6e7b0351f43a'::uuid, c.id, 1978, TIMESTAMPTZ '1978-08-15 00:00:00+00', TIMESTAMPTZ '1978-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Sierra de Segura')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1978)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'd5a52995-a6c8-412d-9308-39d006dd49b2'::uuid, c.id, 1979, TIMESTAMPTZ '1979-08-15 00:00:00+00', TIMESTAMPTZ '1979-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Montes Universales')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1979)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '855f31d0-d523-463a-b350-e7fb81591703'::uuid, c.id, 1980, TIMESTAMPTZ '1980-08-15 00:00:00+00', TIMESTAMPTZ '1980-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('San Pedro de las Herrerías')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1980)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'f207c7ba-80ef-4680-8625-d648a4cc89c5'::uuid, c.id, 1981, TIMESTAMPTZ '1981-08-15 00:00:00+00', TIMESTAMPTZ '1981-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Sierra del Moncayo')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1981)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'ed9738f8-6cf3-492a-8d40-78111ece5c03'::uuid, c.id, 1982, TIMESTAMPTZ '1982-08-15 00:00:00+00', TIMESTAMPTZ '1982-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Boñar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1982)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '15f6df9d-d3d0-40a0-a83f-83be757edbf1'::uuid, c.id, 1983, TIMESTAMPTZ '1983-08-15 00:00:00+00', TIMESTAMPTZ '1983-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Espinosa de los Monteros')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1983)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '3410b95f-0e5f-4523-9677-0e3b34eba94e'::uuid, c.id, 1984, TIMESTAMPTZ '1984-08-15 00:00:00+00', TIMESTAMPTZ '1984-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Sierra de San Vicente')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1984)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '247f962d-4fe5-4b68-aa9f-58080c689c51'::uuid, c.id, 1985, TIMESTAMPTZ '1985-08-15 00:00:00+00', TIMESTAMPTZ '1985-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Ulzama')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1985)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '6b849351-6176-42b8-b42d-67142c393e1d'::uuid, c.id, 1986, TIMESTAMPTZ '1986-08-15 00:00:00+00', TIMESTAMPTZ '1986-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Jerte')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1986)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'db327414-acac-4b0e-9671-6f2a25f85e8b'::uuid, c.id, 1987, TIMESTAMPTZ '1987-08-15 00:00:00+00', TIMESTAMPTZ '1987-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Los Palancares')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1987)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '34a5e00b-7fa9-41d6-a277-805dbd9674f3'::uuid, c.id, 1988, TIMESTAMPTZ '1988-08-15 00:00:00+00', TIMESTAMPTZ '1988-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Mora de Rubielos')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1988)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'c4e07cd6-ef67-4e92-8b83-058d5f48af3b'::uuid, c.id, 1989, TIMESTAMPTZ '1989-08-15 00:00:00+00', TIMESTAMPTZ '1989-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Sierra de San Vicente')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1989)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '0b1787c8-6141-4247-9486-1eacffd76771'::uuid, c.id, 1990, TIMESTAMPTZ '1990-08-15 00:00:00+00', TIMESTAMPTZ '1990-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Montes de Talayuelas')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1990)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '6eb9a0ea-f7e1-47f4-bba6-c0a132971166'::uuid, c.id, 1991, TIMESTAMPTZ '1991-08-15 00:00:00+00', TIMESTAMPTZ '1991-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Montes Universales')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1991)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '08beb364-5f52-43e8-912f-a928e35a1db8'::uuid, c.id, 1992, TIMESTAMPTZ '1992-08-15 00:00:00+00', TIMESTAMPTZ '1992-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Pola de Gordon')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1992)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '0a8e3997-6342-4970-bcb7-3f666f76b75b'::uuid, c.id, 1993, TIMESTAMPTZ '1993-08-15 00:00:00+00', TIMESTAMPTZ '1993-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Espinosa de los Monteros')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1993)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'df0293ca-c4e2-4983-9163-789f0c9ec280'::uuid, c.id, 1994, TIMESTAMPTZ '1994-08-15 00:00:00+00', TIMESTAMPTZ '1994-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Los Palancares')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1994)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '219e53c0-f06f-4952-9169-90a5d467c081'::uuid, c.id, 1995, TIMESTAMPTZ '1995-08-15 00:00:00+00', TIMESTAMPTZ '1995-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Covaleda')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1995)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '9da0cf6e-250f-49d5-baee-6630885de321'::uuid, c.id, 1996, TIMESTAMPTZ '1996-08-15 00:00:00+00', TIMESTAMPTZ '1996-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Condemios de Arriba')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1996)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'c0dd216d-f8ea-4d1b-afd8-1b370cfb0f89'::uuid, c.id, 1997, TIMESTAMPTZ '1997-08-15 00:00:00+00', TIMESTAMPTZ '1997-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Selva de Oza')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1997)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'dca3e461-d5e6-43d5-b937-eb0dbf0d7e54'::uuid, c.id, 1998, TIMESTAMPTZ '1998-08-15 00:00:00+00', TIMESTAMPTZ '1998-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('El Bosque')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1998)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'cf1e2f45-6906-4790-84cc-76651d8c5dca'::uuid, c.id, 1999, TIMESTAMPTZ '1999-08-15 00:00:00+00', TIMESTAMPTZ '1999-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Ulzama')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 1999)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '9beace08-4e81-4fba-8b58-a25815b51678'::uuid, c.id, 2000, TIMESTAMPTZ '2000-08-15 00:00:00+00', TIMESTAMPTZ '2000-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('San Juan de Riópar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2000)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '4ef0e718-d274-44af-bb7b-88a93397971d'::uuid, c.id, 2001, TIMESTAMPTZ '2001-08-15 00:00:00+00', TIMESTAMPTZ '2001-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('San Pedro de las Herrerías')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2001)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '3c1caf4d-514b-4105-b21e-40603e815159'::uuid, c.id, 2002, TIMESTAMPTZ '2002-08-15 00:00:00+00', TIMESTAMPTZ '2002-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('San Martín del Castañar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2002)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '7af50da8-97c8-4c87-a3fa-3ff052c03478'::uuid, c.id, 2003, TIMESTAMPTZ '2003-08-15 00:00:00+00', TIMESTAMPTZ '2003-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Espinosa de los Monteros')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2003)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '4a4d1fd8-8330-4872-9707-67dc4b3abbef'::uuid, c.id, 2004, TIMESTAMPTZ '2004-08-15 00:00:00+00', TIMESTAMPTZ '2004-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Condemios de Arriba')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2004)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'a2a3b6bd-7b63-4cb7-83d1-e2de29794ab7'::uuid, c.id, 2005, TIMESTAMPTZ '2005-08-15 00:00:00+00', TIMESTAMPTZ '2005-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Cabañeros')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2005)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '0f9c1286-c912-401b-8fe9-57cb9c37e66f'::uuid, c.id, 2006, TIMESTAMPTZ '2006-08-15 00:00:00+00', TIMESTAMPTZ '2006-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Boñar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2006)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'b0e66cd2-986c-4b46-a134-d7594eee3b26'::uuid, c.id, 2007, TIMESTAMPTZ '2007-08-15 00:00:00+00', TIMESTAMPTZ '2007-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('San Martín del Castañar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2007)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '74e780e7-8729-45e1-b028-aaa90ec4478b'::uuid, c.id, 2008, TIMESTAMPTZ '2008-08-15 00:00:00+00', TIMESTAMPTZ '2008-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Quintanar de la Sierra')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2008)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'e0dcd388-7412-4388-9d67-4d34a944332a'::uuid, c.id, 2009, TIMESTAMPTZ '2009-08-15 00:00:00+00', TIMESTAMPTZ '2009-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Boñar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2009)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'fc94e6d0-478b-4b76-9680-104fb01b5dd2'::uuid, c.id, 2010, TIMESTAMPTZ '2010-08-15 00:00:00+00', TIMESTAMPTZ '2010-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Oto')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2010)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '35e28572-7e54-42a6-a2c4-08bf49b5164c'::uuid, c.id, 2011, TIMESTAMPTZ '2011-08-15 00:00:00+00', TIMESTAMPTZ '2011-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Quintanar de la Sierra')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2011)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '58cd8b71-b9a4-4dd1-b61e-1fe4b78685a2'::uuid, c.id, 2012, TIMESTAMPTZ '2012-08-15 00:00:00+00', TIMESTAMPTZ '2012-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Molino de Butrera')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2012)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '43e3d9e1-4873-4ead-9378-fba55ecc6f88'::uuid, c.id, 2013, TIMESTAMPTZ '2013-08-15 00:00:00+00', TIMESTAMPTZ '2013-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('San Pedro de las Herrerías')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2013)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'cc55dcd8-af47-48e8-a643-f9a58680efcf'::uuid, c.id, 2014, TIMESTAMPTZ '2014-08-15 00:00:00+00', TIMESTAMPTZ '2014-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Casillas de Ranera')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2014)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '9643cc86-be20-4b54-9d17-3ea5693fbc26'::uuid, c.id, 2015, TIMESTAMPTZ '2015-08-15 00:00:00+00', TIMESTAMPTZ '2015-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Espinosa de los Monteros')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2015)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'a57ee069-66f5-46d6-9377-e19919a684b1'::uuid, c.id, 2016, TIMESTAMPTZ '2016-08-15 00:00:00+00', TIMESTAMPTZ '2016-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Cervera de Pisuerga')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2016)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '104f1117-7963-480d-85e2-2ca2a4c40948'::uuid, c.id, 2017, TIMESTAMPTZ '2017-08-15 00:00:00+00', TIMESTAMPTZ '2017-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Palacio de la Teyeria, Mestas de Con')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2017)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '0d349479-0737-4f35-9490-e66a537d3fa3'::uuid, c.id, 2018, TIMESTAMPTZ '2018-08-15 00:00:00+00', TIMESTAMPTZ '2018-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Condemios de Arriba')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2018)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'b737f27e-0d94-41c2-8354-be7f66118b03'::uuid, c.id, 2019, TIMESTAMPTZ '2019-08-15 00:00:00+00', TIMESTAMPTZ '2019-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Villamanín')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2019)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'c19c13fe-3740-4ecd-8b53-0bb731bf2a32'::uuid, c.id, 2020, TIMESTAMPTZ '2020-08-15 00:00:00+00', TIMESTAMPTZ '2020-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Los Palancares')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2020)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'cde30c7f-5ec7-40ad-8e4e-96b0c8389ce9'::uuid, c.id, 2021, TIMESTAMPTZ '2021-08-15 00:00:00+00', TIMESTAMPTZ '2021-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Jerte')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2021)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'afbe3fb7-1ab1-4555-b9db-bdd7f2bd00b2'::uuid, c.id, 2022, TIMESTAMPTZ '2022-08-15 00:00:00+00', TIMESTAMPTZ '2022-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Arcas del Villar')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2022)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'fcdc5a55-821d-4822-a5d8-7b7bf70505ec'::uuid, c.id, 2023, TIMESTAMPTZ '2023-08-15 00:00:00+00', TIMESTAMPTZ '2023-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Condado de Castilnovo')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2023)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT '61bbd5ec-da31-4013-8853-aaf45b43ad36'::uuid, c.id, 2024, TIMESTAMPTZ '2024-08-15 00:00:00+00', TIMESTAMPTZ '2024-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Pola de Gordon')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2024)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO camp_editions (id, camp_id, year, start_date, end_date, price_per_adult, price_per_child, price_per_baby, use_custom_age_ranges, status, max_capacity, is_archived, created_at, updated_at)
            SELECT 'd02133c5-3c77-4c17-9961-90f0bdabad5b'::uuid, c.id, 2025, TIMESTAMPTZ '2025-08-15 00:00:00+00', TIMESTAMPTZ '2025-08-30 00:00:00+00', 0, 0, 0, FALSE, 'Completed', 0, FALSE, TIMESTAMPTZ '2026-08-23 17:35:44+00', TIMESTAMPTZ '2026-08-23 17:35:44+00'
            FROM camps c
            WHERE LOWER(c.name) = LOWER('Matapozuelos')
              AND NOT EXISTS (SELECT 1 FROM camp_editions x WHERE x.camp_id = c.id AND x.year = 2025)
            ON CONFLICT (id) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Editions first: they hold the foreign key. Deleting by the identifiers this
            // migration inserted leaves any venue that already existed untouched.
            migrationBuilder.Sql(@"
            DELETE FROM camp_editions WHERE id IN (
                'ebaaf7b7-2c82-4fa1-9b9a-2fd2996a1194',
                'ded1aa90-e871-4e4d-bc0b-fbce18e3cd05',
                '1b587207-438d-4156-bc14-6e7b0351f43a',
                'd5a52995-a6c8-412d-9308-39d006dd49b2',
                '855f31d0-d523-463a-b350-e7fb81591703',
                'f207c7ba-80ef-4680-8625-d648a4cc89c5',
                'ed9738f8-6cf3-492a-8d40-78111ece5c03',
                '15f6df9d-d3d0-40a0-a83f-83be757edbf1',
                '3410b95f-0e5f-4523-9677-0e3b34eba94e',
                '247f962d-4fe5-4b68-aa9f-58080c689c51',
                '6b849351-6176-42b8-b42d-67142c393e1d',
                'db327414-acac-4b0e-9671-6f2a25f85e8b',
                '34a5e00b-7fa9-41d6-a277-805dbd9674f3',
                'c4e07cd6-ef67-4e92-8b83-058d5f48af3b',
                '0b1787c8-6141-4247-9486-1eacffd76771',
                '6eb9a0ea-f7e1-47f4-bba6-c0a132971166',
                '08beb364-5f52-43e8-912f-a928e35a1db8',
                '0a8e3997-6342-4970-bcb7-3f666f76b75b',
                'df0293ca-c4e2-4983-9163-789f0c9ec280',
                '219e53c0-f06f-4952-9169-90a5d467c081',
                '9da0cf6e-250f-49d5-baee-6630885de321',
                'c0dd216d-f8ea-4d1b-afd8-1b370cfb0f89',
                'dca3e461-d5e6-43d5-b937-eb0dbf0d7e54',
                'cf1e2f45-6906-4790-84cc-76651d8c5dca',
                '9beace08-4e81-4fba-8b58-a25815b51678',
                '4ef0e718-d274-44af-bb7b-88a93397971d',
                '3c1caf4d-514b-4105-b21e-40603e815159',
                '7af50da8-97c8-4c87-a3fa-3ff052c03478',
                '4a4d1fd8-8330-4872-9707-67dc4b3abbef',
                'a2a3b6bd-7b63-4cb7-83d1-e2de29794ab7',
                '0f9c1286-c912-401b-8fe9-57cb9c37e66f',
                'b0e66cd2-986c-4b46-a134-d7594eee3b26',
                '74e780e7-8729-45e1-b028-aaa90ec4478b',
                'e0dcd388-7412-4388-9d67-4d34a944332a',
                'fc94e6d0-478b-4b76-9680-104fb01b5dd2',
                '35e28572-7e54-42a6-a2c4-08bf49b5164c',
                '58cd8b71-b9a4-4dd1-b61e-1fe4b78685a2',
                '43e3d9e1-4873-4ead-9378-fba55ecc6f88',
                'cc55dcd8-af47-48e8-a643-f9a58680efcf',
                '9643cc86-be20-4b54-9d17-3ea5693fbc26',
                'a57ee069-66f5-46d6-9377-e19919a684b1',
                '104f1117-7963-480d-85e2-2ca2a4c40948',
                '0d349479-0737-4f35-9490-e66a537d3fa3',
                'b737f27e-0d94-41c2-8354-be7f66118b03',
                'c19c13fe-3740-4ecd-8b53-0bb731bf2a32',
                'cde30c7f-5ec7-40ad-8e4e-96b0c8389ce9',
                'afbe3fb7-1ab1-4555-b9db-bdd7f2bd00b2',
                'fcdc5a55-821d-4822-a5d8-7b7bf70505ec',
                '61bbd5ec-da31-4013-8853-aaf45b43ad36',
                'd02133c5-3c77-4c17-9961-90f0bdabad5b'
            );
            ");

            migrationBuilder.Sql(@"
            DELETE FROM camps WHERE id IN (
                'fca8644d-33a7-5fb7-a74a-9e3a426ef8fc',
                '2d01acba-c387-50af-915b-38a05d0d4712',
                '3f25df97-5fad-5e0d-8baf-72773ad40632',
                '56b12806-efc9-56fd-ba6e-ffa2a5e18dcc',
                '0e940eea-3792-53d2-a944-b41f39f81f43',
                'f7792268-54ac-5065-a1e4-79cf321eeeff',
                '8c9f723d-df6d-5769-aeac-12a198a352b3',
                '936e008f-1193-5ece-bd42-422119c019f4',
                'c5ed2f53-406f-501b-a22c-a450211c099c',
                '2e24b0b2-d59c-5bde-a556-53a40af78064',
                'aa98df4b-bdfc-5d0e-8bce-8427d204bcf1',
                '1abbb90d-fed7-5b74-b218-a2efdd1fd337',
                'bccef80e-6da8-5faa-a3e6-cf9757fb9b0d',
                '1819f627-0817-5be3-b060-9e1bca742115',
                'da9a1589-4267-5061-bae0-8df95de05eb4',
                '580f6bdd-4cd3-5651-8972-3c02bdd9bb07',
                '6ffc306b-8898-5b27-a7b8-9030558097e5',
                'a030d43d-7ccd-5622-bbe8-7dee4f372cb9',
                'cc08adc4-ac6c-521b-bccd-f70060367bd1',
                '4ab51f0f-3eed-50e4-b096-3f65098b89a3',
                'f0d01b56-1673-5439-9eca-790440e72be6',
                '9ddaa033-b8b1-5986-85cb-9bb53b47cf7c',
                '406b0edb-366f-54d7-8f71-1f856371c3c4',
                '185e3b5e-4a4f-5f06-b67c-23d04e69fb4e',
                '8449e2be-827e-5a52-a98b-8ed69a5f13db',
                'fea1dfaf-c442-5803-898e-8447d7351b6f',
                '6b38253e-4bd9-554b-98c9-709bfcad63d6',
                '4b209c84-2bda-503f-b053-fa3eaa90bad8',
                '39f5ff0c-e652-57d5-bfac-8ee52d0039b4',
                'e13bca3a-3a05-53a1-b74d-491adeb3cf40',
                'cf195596-1570-558f-97f9-1b66f7665b2a'
            );
            ");
        }
    }
}
