# Adding extra characteristics to camps from external source

## Summary

Given the external source, we want to be able to import all the characteristics included in file `CAMPAMENTOS.csv` and add them to the existing camps in our system. This will allow us to have more detailed information about each camp, which can be useful for various purposes such as analysis, reporting, and improving the user experience.

## Steps to Implement

1. **Data Mapping**: First, we need to analyze the structure of the `CAMPAMENTOS.csv` file and identify the characteristics that are included. We will then map these characteristics to the corresponding fields in our existing camp data model.
2. **Extend the Camp data model** to include the new characteristics. This may involve adding new fields to the database schema and updating any relevant data structures in the codebase.
3. **Data Import**: Develop a script or tool to read the `CAMPAMENTOS.csv` file and import the data into our system. This will involve parsing the CSV file, extracting the relevant information, and updating the existing camp records with the new characteristics.
4. **Data Validation**: Implement validation checks to ensure that the data being imported is accurate and consistent with our existing data. This may include checks for data types, required fields, and any business rules that apply to the characteristics being imported.
5. **Testing**: Thoroughly test the import process to ensure that it works correctly and that the new characteristics are being added to the camps as expected. This may involve creating test cases with sample data and verifying the results.

File `CAMPAMENTOS.csv` is here [CAMPAMENTOS.CSV](./CAMPAMENTOS.CSV) está en codificación ISO-8859-1, por lo que se debe tener en cuenta esta codificación al momento de leer el archivo para evitar problemas con caracteres especiales.
