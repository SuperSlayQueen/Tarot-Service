using FluentValidation;
using test_project.Models.DTO;

namespace test_project.Validators;

public class CreateCardDtoValidator : AbstractValidator<CreateCardDto>
{
    public CreateCardDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название карты обязательно")
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов");

        RuleFor(x => x.Suit)
            .NotEmpty().WithMessage("Масть обязательна")
            .MaximumLength(100).WithMessage("Масть не должна превышать 100 символов");

        RuleFor(x => x.Number)
            .GreaterThanOrEqualTo(0).When(x => x.Number.HasValue)
            .WithMessage("Номер карты должен быть неотрицательным");
    }
}
