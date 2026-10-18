namespace Inv_Property_Tab.Configuration
{
    public sealed class FieldsConfigurationStore
    {
        public FieldsConfiguration Load()
        {
            return new FieldsConfiguration();
        }

        public void Save(FieldsConfiguration configuration)
        {
        }
    }
}
