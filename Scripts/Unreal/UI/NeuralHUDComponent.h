UCLASS()
class NEURALUI_API UNeuralHUDComponent : public UActorComponent
{
    GENERATED_BODY()
    
    UFUNCTION(BlueprintCallable)
    void UpdateContextualActions(FVector2D TouchDelta);
};