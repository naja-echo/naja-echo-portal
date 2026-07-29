import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ShipsImportTab } from '../components/ShipsImportTab'
import { ItemsImportTab } from '../components/ItemsImportTab'
import { CommoditiesImportTab } from '../components/CommoditiesImportTab'
import { LocationsImportTab } from '../components/LocationsImportTab'
import { BlueprintsImportTab } from '../components/BlueprintsImportTab'

export function DataImportPage() {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">Data Import</h1>
        <p className="text-muted-foreground">Import game data from the UEX Corp feed.</p>
      </div>

      <Tabs defaultValue="ships">
        <TabsList>
          <TabsTrigger value="ships">Ships</TabsTrigger>
          <TabsTrigger value="items">Items</TabsTrigger>
          <TabsTrigger value="commodities">Commodities</TabsTrigger>
          <TabsTrigger value="locations">Locations</TabsTrigger>
          <TabsTrigger value="blueprints">Blueprints</TabsTrigger>
        </TabsList>
        <TabsContent value="ships">
          <ShipsImportTab />
        </TabsContent>
        <TabsContent value="items">
          <ItemsImportTab />
        </TabsContent>
        <TabsContent value="commodities">
          <CommoditiesImportTab />
        </TabsContent>
        <TabsContent value="locations">
          <LocationsImportTab />
        </TabsContent>
        <TabsContent value="blueprints">
          <BlueprintsImportTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}
